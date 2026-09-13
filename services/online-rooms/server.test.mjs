import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createRoomServer } from './server.mjs';
const key = 'a'.repeat(32);
async function fixture(options = {}) {
  const server = createRoomServer({ accessKey: key, ...options });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const url = `http://127.0.0.1:${server.address().port}`;
  return {
    async request(path, method = 'GET', body, participantToken, accessKey = key) {
      const response = await fetch(url + path, { method, headers: { Authorization: `Bearer ${accessKey}`,
        'Content-Type': 'application/json', ...(participantToken ? { 'X-Room-Token': participantToken } : {}) },
        body: body === undefined ? undefined : JSON.stringify(body) });
      return { status: response.status, data: await response.json() };
    },
    close: () => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }),
  };
}
test('private two-player room exchanges descriptions and rejects extra participants', async () => {
  const f = await fixture();
  try {
    assert.equal((await f.request('/v1/rooms', 'POST', { profile: 'gb-serial-v1' }, null, 'wrong')).status, 401);
    const host = (await f.request('/v1/rooms', 'POST', { profile: 'gb-serial-v1' })).data;
    const room = '/v1/rooms/' + host.code;
    assert.equal(host.code.length, 10);
    assert.equal((await f.request(room)).status, 403);
    assert.equal((await f.request(room + '/join', 'POST', { profile: 'gba-pokemon-gen3-v1' })).status, 409);
    const guest = (await f.request(room + '/join', 'POST', { profile: 'gb-serial-v1' })).data;
    assert.equal((await f.request(room + '/join', 'POST', { profile: 'gb-serial-v1' })).status, 409);
    const offer = { type: 'offer', sdp: 'v=0\r\nm=application 9 UDP/DTLS/SCTP webrtc-datachannel\r\n' };
    assert.equal((await f.request(room + '/description', 'PUT', offer, guest.participantToken)).status, 400);
    assert.equal((await f.request(room + '/description', 'PUT', offer, host.participantToken)).status, 200);
    assert.deepEqual((await f.request(room, 'GET', undefined, guest.participantToken)).data.remoteDescription, offer);
    assert.equal((await f.request(room + '/description', 'PUT', { ...offer, sdp: offer.sdp + 'a=changed\r\n' }, host.participantToken)).status, 409);
    assert.equal((await f.request(room, 'DELETE', undefined, guest.participantToken)).status, 200);
    assert.equal((await f.request(room, 'GET', undefined, host.participantToken)).status, 404);
  } finally { await f.close(); }
});
test('expired rooms disappear and simultaneous guests cannot both join', async () => {
  let now = 1; const f = await fixture({ now: () => now, ttlMs: 100 });
  try {
    const host = (await f.request('/v1/rooms', 'POST', { profile: 'gb-serial-v1' })).data;
    const path = '/v1/rooms/' + host.code;
    const results = await Promise.all([1, 2].map(() => f.request(path + '/join', 'POST', { profile: 'gb-serial-v1' })));
    assert.deepEqual(results.map(r => r.status).sort(), [200, 409]);
    now = 102;
    assert.equal((await f.request(path, 'GET', undefined, host.participantToken)).status, 404);
  } finally { await f.close(); }
});
test('media descriptions and excess rooms are rejected', async () => {
  const f = await fixture({ maxRooms: 1 });
  try {
    const host = (await f.request('/v1/rooms', 'POST', { profile: 'gb-serial-v1' })).data;
    assert.equal((await f.request('/v1/rooms', 'POST', { profile: 'gb-serial-v1' })).status, 503);
    assert.equal((await f.request('/v1/rooms/' + host.code + '/description', 'PUT', {
      type: 'offer', sdp: 'm=application 9 test\r\nm=audio 9 test',
    }, host.participantToken)).status, 400);
  } finally { await f.close(); }
});
