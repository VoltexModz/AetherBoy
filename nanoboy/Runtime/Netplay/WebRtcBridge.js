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

  function controls() {
    const localReady = bridge?.readyState === WebSocket.OPEN && !ended;
    byId("offer").disabled = !localReady || busy || peer !== null;
    byId("answer").disabled = !localReady || busy || peer !== null;
    byId("accept").disabled = !localReady || busy || peer?.signalingState !== "have-local-offer";
    byId("copy").disabled = !byId("local").value;
  }

  function stop(message, failure = false) {
    if (ended) return;
    ended = true;
    if (sendTimer !== null) clearInterval(sendTimer);
    sendTimer = null;
    toPeer.length = 0; toNative.length = 0;
    if (bridge?.readyState === WebSocket.OPEN) {
      bridge.send(failure ? "ERROR" : "CLOSED");
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
      stop("Die Datenverbindung ist fehlgeschlagen. Neue Sitzung im Emulator öffnen.", true);
    }
  }

  function enqueue(queue, packet) {
    if (!checkPacket(packet) || queue.length >= maxPackets) {
      stop("Ungültige oder zu viele Link-Pakete. Die Sitzung wurde sicher beendet.", true);
      return;
    }
    queue.push(packet);
    pump();
  }

  function bindChannel(candidate) {
    if (channel !== null || candidate.label !== channelName || candidate.protocol !== channelName ||
        !candidate.ordered || candidate.maxRetransmits !== null || candidate.maxPacketLifeTime !== null) {
      candidate.close();
      stop("Der Mitspieler verwendet keinen passenden zuverlässigen AetherBoy-Datenkanal.", true);
      return;
    }
    channel = candidate;
    channel.binaryType = "arraybuffer";
    channel.bufferedAmountLowThreshold = 0;
    channel.onbufferedamountlow = pump;
    channel.onopen = () => {
      if (ended || bridge?.readyState !== WebSocket.OPEN) {
        stop("Die lokale Emulator-Verbindung ist nicht mehr verfügbar.", true);
        return;
      }
      bridge.send("READY");
      status("Verbunden · Zum Emulator zurückkehren und dort weiterspielen.");
      sendTimer = setInterval(pump, 8);
      controls();
    };
    channel.onmessage = event => enqueue(toNative, event.data);
    channel.onerror = () => stop("Der WebRTC-Datenkanal hat einen Fehler gemeldet.", true);
    channel.onclose = () => stop("Mitspieler getrennt. Für einen neuen Versuch eine neue Sitzung öffnen.");
  }

  function configuration() {
    const stun = byId("stun").value.trim(), turn = byId("turn").value.trim();
    if ((stun || turn) && !byId("serverConsent").checked)
      throw new Error("Bitte den optionalen Serverkontakt ausdrücklich bestätigen oder die Serverfelder leeren.");
    if (stun && !/^stuns?:[a-z0-9.\-:\[\]]{1,255}$/i.test(stun))
      throw new Error("Die STUN-Adresse ist ungültig. Erwartet wird stun:server:port.");
    if (turn && !/^turns?:[a-z0-9.\-:\[\]]{1,255}(?:\?transport=(?:udp|tcp))?$/i.test(turn))
      throw new Error("Die TURN-Adresse ist ungültig. Erwartet wird turn:server:port.");
    if (byId("relayOnly").checked && !turn)
      throw new Error("Für den reinen Relay-Modus muss ein eigener TURN-Server angegeben werden.");
    const iceServers = [];
    if (stun) iceServers.push({ urls: stun });
    if (turn) iceServers.push({ urls: turn, username: byId("turnUser").value, credential: byId("turnPassword").value });
    return { iceServers, iceTransportPolicy: byId("relayOnly").checked ? "relay" : "all", bundlePolicy: "max-bundle" };
  }

  function newPeer() {
    if (peer !== null) throw new Error("Diese Sitzung wurde bereits vorbereitet. Für einen Neustart die Sitzung im Emulator neu öffnen.");
    if (typeof RTCPeerConnection !== "function") throw new Error("Dieser Browser unterstützt WebRTC nicht.");
    peer = new RTCPeerConnection(configuration());
    peer.ondatachannel = event => bindChannel(event.channel);
    peer.onconnectionstatechange = () => {
      if (peer.connectionState === "failed")
        stop("Keine Peer-Verbindung möglich. Router/Firewall prüfen; gegebenenfalls einen eigenen STUN/TURN-Server verwenden.", true);
      else if (peer.connectionState === "disconnected" || peer.connectionState === "closed")
        stop("Die Peer-Verbindung wurde unterbrochen. Neue Sitzung im Emulator öffnen.");
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
      const timeout = setTimeout(() => finish(new Error("Die Suche nach Verbindungswegen dauerte zu lange. Server/Netzwerk prüfen und eine neue Sitzung öffnen.")), 45000);
      pc.addEventListener("icegatheringstatechange", changed);
      changed();
    });
  }

  function readDescription(expected) {
    const text = byId("remote").value.trim();
    if (!text || text.length > 131072) throw new Error("Bitte passende Verbindungsdaten einfügen (maximal 128 KiB).");
    let data;
    try { data = JSON.parse(text); } catch { throw new Error("Die eingefügten Daten sind kein gültiges JSON."); }
    if (!data || data.format !== "aetherboy-webrtc" || data.version !== 1 || data.type !== expected ||
        typeof data.sdp !== "string" || data.sdp.length > 100000 || !/^m=application /m.test(data.sdp) || /^m=(audio|video) /m.test(data.sdp))
      throw new Error("Erwartet wird eine AetherBoy-" + (expected === "offer" ? "Einladung" : "Antwort") + " für einen reinen Datenkanal.");
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
    catch (error) { status(error instanceof Error ? error.message : "Die Verbindung konnte nicht vorbereitet werden."); }
    finally { busy = false; controls(); }
  }

  byId("offer").addEventListener("click", () => action(async () => {
    const pc = newPeer();
    bindChannel(pc.createDataChannel(channelName, { ordered: true, protocol: channelName }));
    status("Einladung wird vorbereitet · Verbindungswege werden gesammelt …");
    await pc.setLocalDescription(await pc.createOffer());
    await gatherIce(pc);
    if (ended) return;
    exportDescription(pc);
    status("Einladung bereit · Kopieren, an den Mitspieler weitergeben und seine Antwort einfügen.");
  }));

  byId("answer").addEventListener("click", () => action(async () => {
    const description = readDescription("offer");
    const pc = newPeer();
    status("Antwort wird vorbereitet · Verbindungswege werden gesammelt …");
    await pc.setRemoteDescription(description);
    await pc.setLocalDescription(await pc.createAnswer());
    await gatherIce(pc);
    if (ended) return;
    exportDescription(pc);
    status("Antwort bereit · Kopieren und an den einladenden Spieler zurückgeben.");
  }));

  byId("accept").addEventListener("click", () => action(async () => {
    const description = readDescription("answer");
    if (peer?.signalingState !== "have-local-offer") throw new Error("Bitte zuerst eine eigene Einladung erstellen.");
    await peer.setRemoteDescription(description);
    status("Antwort übernommen · Direkte Verbindung wird aufgebaut …");
  }));

  byId("copy").addEventListener("click", async () => {
    byId("local").focus(); byId("local").select();
    try {
      await navigator.clipboard.writeText(byId("local").value);
      status("Verbindungsdaten kopiert. Nur mit deinem Mitspieler teilen.");
    } catch { status("Verbindungsdaten sind markiert. Bitte mit Strg+C kopieren."); }
  });
  byId("disconnect").addEventListener("click", () => stop("Verbindung beendet. Diese Seite kann geschlossen werden."));
  window.addEventListener("pagehide", () => stop("Seite geschlossen."));

  if (!/^[0-9A-F]{64}$/.test(token) || location.hostname !== "127.0.0.1" || location.protocol !== "http:") {
    stop("Diese lokale Seite muss aus einer laufenden AetherBoy-Online-Link-Sitzung geöffnet werden.");
    return;
  }
  bridge = new WebSocket("ws://" + location.host + "/bridge?token=" + token);
  bridge.binaryType = "arraybuffer";
  bridge.onopen = () => { status("Lokaler Emulator verbunden · Einladung erstellen oder eine Einladung beantworten."); controls(); };
  bridge.onmessage = event => {
    if (channel?.readyState !== "open") { stop("Emulator-Daten kamen vor einer bereiten Peer-Verbindung an.", true); return; }
    enqueue(toPeer, event.data);
  };
  bridge.onerror = () => stop("Die lokale Emulator-Verbindung ist nicht erreichbar. Neue Sitzung im Emulator öffnen.", true);
  bridge.onclose = () => stop("Die Emulator-Verbindung wurde geschlossen. Neue Sitzung im Emulator öffnen.");
  controls();
})();
