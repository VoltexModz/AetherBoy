// Run with: node --test tests/browser/WebRtcBridge.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');
const script = readFileSync(join(__dirname, '../../nanoboy/Runtime/Netplay/WebRtcBridge.js'), 'utf8');

function helper() {
  const elements = new Map();
  const element = id => {
    if (!elements.has(id)) elements.set(id, {
      value: '', checked: false, textContent: '', handlers: {},
      addEventListener(name, callback) { this.handlers[name] = callback; },
      focus() {}, select() {},
    });
    return elements.get(id);
  };
  let socket, peer, finishCopy, duringRemote = () => {};
  class Socket {
    static OPEN = 1;
    constructor() { socket = this; this.readyState = 1; this.sent = []; }
    send(data) { this.sent.push(data); }
    close() { this.readyState = 3; this.onclose?.(); }
  }
  class Peer {
    constructor() {
      peer = this;
      this.iceGatheringState = 'complete';
      this.signalingState = 'have-local-offer';
    }
    createDataChannel(label, options) {
      this.channel = {
        label, ...options, maxRetransmits: null, maxPacketLifeTime: null, readyState: 'connecting',
        close() { this.readyState = 'closed'; this.onclose?.(); },
      };
      return this.channel;
    }
    async createOffer() { return { type: 'offer', sdp: 'm=application test' }; }
    async setLocalDescription(description) { this.localDescription = description; }
    async setRemoteDescription() { duringRemote(); }
    addEventListener() {}
    removeEventListener() {}
    close() { this.connectionState = 'closed'; this.onconnectionstatechange?.(); }
  }
  vm.runInNewContext(script, {
    document: { getElementById: element }, WebSocket: Socket, RTCPeerConnection: Peer,
    location: { hash: '#' + 'A'.repeat(64), pathname: '/', hostname: '127.0.0.1', protocol: 'http:', host: '127.0.0.1:1' },
    history: { replaceState() {} }, window: { addEventListener() {} },
    navigator: { clipboard: { writeText: () => new Promise(resolve => { finishCopy = resolve; }) } },
    setTimeout, clearTimeout, setInterval: () => 1, clearInterval() {},
  });
  socket.onopen();
  return {
    element, socket,
    get peer() { return peer; },
    get status() { return element('status').textContent; },
    click: id => element(id).handlers.click(),
    finishCopy: () => finishCopy(),
    duringRemote: callback => { duringRemote = callback; },
  };
}

test('peer failure reaches native bridge and survives pending clipboard completion', async () => {
  const page = helper();
  await page.click('offer');
  const copying = page.click('copy');
  page.peer.connectionState = 'failed';
  page.peer.onconnectionstatechange();
  assert.deepEqual(page.socket.sent, ['ERROR:PEER_CONNECTION']);
  const failure = page.status;
  assert.match(failure, /TURN-Server/);
  page.finishCopy();
  await copying;
  assert.equal(page.status, failure);
});

test('channel failure carries a distinct reason and preserves the first failure', async () => {
  const page = helper();
  await page.click('offer');
  page.peer.channel.onerror();
  assert.deepEqual(page.socket.sent, ['ERROR:DATA_CHANNEL']);
  assert.match(page.status, /Datenkanal.*Fehler/);
});

for (const outcome of ['connected', 'failed']) {
  test(`answer completion cannot overwrite ${outcome} status`, async () => {
    const page = helper();
    await page.click('offer');
    page.element('remote').value = JSON.stringify({
      format: 'aetherboy-webrtc', version: 1, type: 'answer', sdp: 'm=application test',
    });
    page.duringRemote(() => {
      if (outcome === 'connected') {
        page.peer.channel.readyState = 'open';
        page.peer.channel.onopen();
      } else {
        page.peer.connectionState = 'failed';
        page.peer.onconnectionstatechange();
      }
    });
    await page.click('accept');
    assert.match(page.status, outcome === 'connected' ? /^Verbunden/ : /^Keine Peer-Verbindung/);
    assert.deepEqual(page.socket.sent, [outcome === 'connected' ? 'READY' : 'ERROR:PEER_CONNECTION']);
  });
}
