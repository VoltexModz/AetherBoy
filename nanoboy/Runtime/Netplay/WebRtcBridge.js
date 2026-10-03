"use strict";
(() => {
  const maxPacketBytes = 4096, maxPackets = 128;
  const channelName = "aetherboy-link-v1";
  const byId = id => document.getElementById(id);
  const status = message => { byId("status").textContent = message; };
  const token = location.hash.substring(1);
  // The token is a local capability, never part of signaling or a referrer.
  history.replaceState(null, "", location.pathname);
  let bridge = null, peer = null, channel = null, ended = false, busy = false;
  let sendTimer = null;
  const toPeer = [], toNative = [];
  // Async signaling and clipboard completions must not hide a failure or an open link.
  const progress = message => { if (!ended && channel?.readyState !== "open") status(message); };

  function controls() {
    const localReady = bridge?.readyState === WebSocket.OPEN && !ended;
    byId("offer").disabled = !localReady || busy || peer !== null;
    byId("answer").disabled = !localReady || busy || peer !== null;
    byId("accept").disabled = !localReady || busy || peer?.signalingState !== "have-local-offer";
    byId("copy").disabled = !byId("local").value;
  }

  function stop(message, failure = false, reason = "UNKNOWN") {
    if (ended) return;
    ended = true;
    if (sendTimer !== null) clearInterval(sendTimer);
    sendTimer = null;
    toPeer.length = 0; toNative.length = 0;
    if (bridge?.readyState === WebSocket.OPEN) {
      // Only fixed codes cross the local bridge, never SDP, server credentials or browser errors.
      bridge.send(failure ? "ERROR:" + reason : "CLOSED");
      bridge.close(1000, "Link closed");
    }
    channel?.close();
    peer?.close();
    status(message);
    controls();
  }

  function checkPacket(data) {
    return data instanceof ArrayBuffer && data.byteLength > 0 && data.byteLength <= maxPacketBytes;
  }

  function pump() {
    if (ended) return;
    try {
      // Keep at most one packet in each browser API's visible send buffer;
      // all application-level waiting queues have a strict packet-count cap.
      while (toPeer.length && channel?.readyState === "open" && channel.bufferedAmount === 0)
        channel.send(toPeer.shift());
      while (toNative.length && bridge?.readyState === WebSocket.OPEN && bridge.bufferedAmount === 0)
        bridge.send(toNative.shift());
    } catch {
      stop(/*ui*/"Die Verbindung ist abgebrochen. Starte im Emulator eine neue Sitzung.", true, "SEND_FAILED");
    }
  }

  function enqueue(queue, packet) {
    if (!checkPacket(packet) || queue.length >= maxPackets) {
      stop(/*ui*/"Die Verbindung wurde wegen ungültiger Daten beendet. Starte eine neue Sitzung.", true, "PACKET_LIMIT");
      return;
    }
    queue.push(packet);
    pump();
  }

  function bindChannel(candidate) {
    if (channel !== null || candidate.label !== channelName || candidate.protocol !== channelName ||
        !candidate.ordered || candidate.maxRetransmits !== null || candidate.maxPacketLifeTime !== null) {
      candidate.close();
      stop(/*ui*/"Der Mitspieler hat einen unpassenden Datenkanal geöffnet. Prüft eure AetherBoy-Versionen.", true, "CHANNEL_PROTOCOL");
      return;
    }
    channel = candidate;
    channel.binaryType = "arraybuffer";
    channel.bufferedAmountLowThreshold = 0;
    channel.onbufferedamountlow = pump;
    channel.onopen = () => {
      if (ended || bridge?.readyState !== WebSocket.OPEN) {
        stop(/*ui*/"Die lokale Emulator-Verbindung ist nicht mehr verfügbar.", true, "LOCAL_CONNECTION");
        return;
      }
      bridge.send("READY");
      status(/*ui*/"Verbunden. Du kannst jetzt im Emulator weiterspielen.");
      sendTimer = setInterval(pump, 8);
      controls();
    };
    channel.onmessage = event => enqueue(toNative, event.data);
    channel.onerror = () => stop(/*ui*/"Der Datenkanal hat einen Fehler gemeldet. Starte im Emulator eine neue Sitzung.", true, "DATA_CHANNEL");
    channel.onclose = () => stop(/*ui*/"Mitspieler getrennt. Für einen neuen Versuch eine neue Sitzung öffnen.");
  }

  function configuration() {
    const stun = byId("stun").value.trim(), turn = byId("turn").value.trim();
    if ((stun || turn) && !byId("serverConsent").checked)
      throw new Error(/*ui*/"Bestätige den Serverkontakt oder entferne die Serveradressen.");
    if (stun && !/^stuns?:[a-z0-9.\-:\[\]]{1,255}$/i.test(stun))
      throw new Error(/*ui*/"Die STUN-Adresse ist ungültig. Erwartet wird stun:server:port.");
    if (turn && !/^turns?:[a-z0-9.\-:\[\]]{1,255}(?:\?transport=(?:udp|tcp))?$/i.test(turn))
      throw new Error(/*ui*/"Die TURN-Adresse ist ungültig. Erwartet wird turn:server:port.");
    if (byId("relayOnly").checked && !turn)
      throw new Error(/*ui*/"Für den reinen Relay-Modus muss ein eigener TURN-Server angegeben werden.");
    const iceServers = [];
    if (stun) iceServers.push({ urls: stun });
    if (turn) iceServers.push({ urls: turn, username: byId("turnUser").value, credential: byId("turnPassword").value });
    return { iceServers, iceTransportPolicy: byId("relayOnly").checked ? "relay" : "all", bundlePolicy: "max-bundle" };
  }

  function newPeer() {
    if (peer !== null) throw new Error(/*ui*/"Diese Sitzung wurde bereits vorbereitet. Für einen Neustart die Sitzung im Emulator neu öffnen.");
    if (typeof RTCPeerConnection !== "function") throw new Error(/*ui*/"Dieser Browser unterstützt WebRTC nicht.");
    peer = new RTCPeerConnection(configuration());
    peer.ondatachannel = event => bindChannel(event.channel);
    peer.onconnectionstatechange = () => {
      if (peer.connectionState === "failed")
        stop(/*ui*/"Keine Peer-Verbindung möglich. STUN/TURN auf beiden Seiten prüfen; bei blockierter Direktverbindung ist ein TURN-Server nötig.", true, "PEER_CONNECTION");
      else if (peer.connectionState === "disconnected" || peer.connectionState === "closed")
        stop(/*ui*/"Die Peer-Verbindung wurde unterbrochen. Neue Sitzung im Emulator öffnen.");
    };
    return peer;
  }

  function gatherIce(pc) {
    return new Promise((resolve, reject) => {
      const finish = error => {
        clearTimeout(timeout);
        pc.removeEventListener("icegatheringstatechange", changed);
        if (error) reject(error); else resolve();
      };
      const changed = () => { if (pc.iceGatheringState === "complete") finish(); };
      const timeout = setTimeout(() => finish(new Error(/*ui*/"Die Verbindung dauert zu lange. Prüfe deine Servereinstellungen und starte eine neue Sitzung.")), 45000);
      pc.addEventListener("icegatheringstatechange", changed);
      changed();
    });
  }

  function readDescription(expected) {
    const text = byId("remote").value.trim();
    if (!text || text.length > 131072) throw new Error(/*ui*/"Füge die Einladung oder Antwort deines Mitspielers ein (maximal 128 KiB).");
    let data;
    try { data = JSON.parse(text); } catch { throw new Error(/*ui*/"Der eingefügte Text ist keine gültige Einladung oder Antwort."); }
    if (!data || data.format !== "aetherboy-webrtc" || data.version !== 1 || data.type !== expected ||
        typeof data.sdp !== "string" || data.sdp.length > 100000 || !/^m=application /m.test(data.sdp) || /^m=(audio|video) /m.test(data.sdp))
      throw new Error(expected === "offer"
        ? /*ui*/"Der eingefügte Text ist keine AetherBoy-Einladung. Bitte kopiere den vollständigen Text von deinem Mitspieler."
        : /*ui*/"Der eingefügte Text ist keine AetherBoy-Antwort. Bitte kopiere den vollständigen Text von deinem Mitspieler.");
    return { type: data.type, sdp: data.sdp };
  }

  function exportDescription(pc) {
    byId("local").value = JSON.stringify({ format: "aetherboy-webrtc", version: 1, type: pc.localDescription.type, sdp: pc.localDescription.sdp });
    controls();
  }

  async function action(work) {
    if (busy || ended) return;
    busy = true; controls();
    try { await work(); }
    // Native browser diagnostics stay verbatim, clearly separated from app copy.
    catch (error) { progress(error instanceof Error ? /*ui*/"Technische Details: " + error.message : /*ui*/"Die Verbindung konnte nicht vorbereitet werden."); }
    finally { busy = false; controls(); }
  }

  byId("offer").addEventListener("click", () => action(async () => {
    const pc = newPeer();
    bindChannel(pc.createDataChannel(channelName, { ordered: true, protocol: channelName }));
    status(/*ui*/"Einladung wird erstellt. Das kann einen Moment dauern …");
    await pc.setLocalDescription(await pc.createOffer());
    await gatherIce(pc);
    if (ended) return;
    exportDescription(pc);
    progress(/*ui*/"Einladung ist fertig. Kopiere sie und füge danach die Antwort deines Mitspielers ein.");
  }));

  byId("answer").addEventListener("click", () => action(async () => {
    const description = readDescription("offer");
    const pc = newPeer();
    status(/*ui*/"Antwort wird erstellt. Das kann einen Moment dauern …");
    await pc.setRemoteDescription(description);
    await pc.setLocalDescription(await pc.createAnswer());
    await gatherIce(pc);
    if (ended) return;
    exportDescription(pc);
    progress(/*ui*/"Antwort ist fertig. Kopiere sie und schick sie an den einladenden Spieler.");
  }));

  byId("accept").addEventListener("click", () => action(async () => {
    const description = readDescription("answer");
    if (peer?.signalingState !== "have-local-offer") throw new Error(/*ui*/"Bitte zuerst eine eigene Einladung erstellen.");
    await peer.setRemoteDescription(description);
    progress(/*ui*/"Antwort übernommen. Verbindung wird aufgebaut …");
  }));

  byId("copy").addEventListener("click", async () => {
    byId("local").focus(); byId("local").select();
    try {
      await navigator.clipboard.writeText(byId("local").value);
      progress(/*ui*/"Verbindungsdaten kopiert. Nur mit deinem Mitspieler teilen.");
    } catch { progress(/*ui*/"Verbindungsdaten sind markiert. Bitte mit Strg+C kopieren."); }
  });
  byId("disconnect").addEventListener("click", () => stop(/*ui*/"Verbindung beendet. Diese Seite kann geschlossen werden."));
  window.addEventListener("pagehide", () => stop(/*ui*/"Seite geschlossen."));

  if (!/^[0-9A-F]{64}$/.test(token) || location.hostname !== "127.0.0.1" || location.protocol !== "http:") {
    stop(/*ui*/"Diese lokale Seite muss aus einer laufenden AetherBoy-Online-Link-Sitzung geöffnet werden.");
    return;
  }
  bridge = new WebSocket("ws://" + location.host + "/bridge?token=" + token);
  bridge.binaryType = "arraybuffer";
  bridge.onopen = () => { status(/*ui*/"Emulator verbunden. Erstelle eine Einladung oder füge die Einladung deines Mitspielers ein."); controls(); };
  bridge.onmessage = event => {
    if (channel?.readyState !== "open") { stop(/*ui*/"Die Verbindung zum Mitspieler war noch nicht bereit. Starte im Emulator eine neue Sitzung.", true, "EARLY_PACKET"); return; }
    enqueue(toPeer, event.data);
  };
  bridge.onerror = () => stop(/*ui*/"Die lokale Emulator-Verbindung ist nicht erreichbar. Neue Sitzung im Emulator öffnen.", true, "LOCAL_CONNECTION");
  bridge.onclose = () => stop(/*ui*/"Die Emulator-Verbindung wurde geschlossen. Neue Sitzung im Emulator öffnen.");
  controls();
})();
