import http from 'node:http';
import { randomBytes, createHash, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';

const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const digest = value => createHash('sha256').update(value).digest();
const same = (a, b) => typeof a === 'string' && timingSafeEqual(digest(a), digest(b));
const token = () => randomBytes(32).toString('base64url');
const code = () => [...randomBytes(10)].map(b => alphabet[b & 31]).join('');
const profiles = new Set(['gb-serial-v1', 'gba-pokemon-gen3-v1', 'transport-probe-v1']);

export function createRoomServer({ accessKey, iceServers = [], ttlMs = 600_000, maxRooms = 100, now = Date.now }) {
  if (typeof accessKey !== 'string' || accessKey.length < 32 || accessKey.length > 256 || !/^[\x21-\x7e]+$/.test(accessKey)) throw new Error('ROOM_ACCESS_KEY needs at least 32 random characters.');
  const rooms = new Map(), limits = new Map();
  function prune() {
    const time = now();
    for (const [id, room] of rooms) if (room.expiresAt <= time) rooms.delete(id);
    for (const [id, limit] of limits) if (limit.reset <= time) limits.delete(id);
  }
  const timer = setInterval(prune, 30_000); timer.unref();
  function reply(res, status, body) {
    res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
    res.end(JSON.stringify(body));
  }
  async function json(req) {
    if (!/^application\/json(?:;|$)/i.test(req.headers['content-type'] ?? '')) throw { status: 415, code: 'json_required' };
    let bytes = 0; const chunks = [];
    for await (const chunk of req) {
      bytes += chunk.length;
      if (bytes > 110_000) throw { status: 413, code: 'request_too_large' };
      chunks.push(chunk);
    }
    try { return JSON.parse(Buffer.concat(chunks).toString()); }
    catch { throw { status: 400, code: 'invalid_json' }; }
  }
  const server = http.createServer(async (req, res) => {
    try {
      prune();
      if (req.url === '/healthz' && req.method === 'GET') return reply(res, 200, { status: 'ok', protocol: 1 });
      // Shared private-instance key; untrusted proxy headers never determine authorization.
      if (!same(req.headers.authorization, `Bearer ${accessKey}`)) return reply(res, 401, { error: 'access_denied' });
      const path = req.url;
      const create = path === '/v1/rooms' && req.method === 'POST';
      const match = /^\/v1\/rooms\/([A-Z2-9]{10})(?:\/(join|description))?$/.exec(path ?? '');
      if (!create && !match) return reply(res, 404, { error: 'not_found' });
      if (create || match?.[2] === 'join') {
        // Global admission limit also bounds guessing when callers share one proxy address.
        const id = 'admission'; let limit = limits.get(id);
        if (!limit || limit.reset <= now()) { limit = { count: 0, reset: now() + 60_000 }; limits.set(id, limit); }
        if (++limit.count > 60) return reply(res, 429, { error: 'try_later' });
      }
      if (create) {
        const body = await json(req);
        if (!profiles.has(body?.profile)) return reply(res, 400, { error: 'unsupported_profile' });
        if (rooms.size >= maxRooms) return reply(res, 503, { error: 'rooms_full' });
        let id; do { id = code(); } while (rooms.has(id));
        const room = { host: token(), guest: null, profile: body.profile, expiresAt: now() + ttlMs, offer: null, answer: null };
        rooms.set(id, room);
        return reply(res, 201, { code: id, participantToken: room.host, expiresAt: room.expiresAt, iceServers });
      }
      const room = rooms.get(match[1]);
      if (!room) return reply(res, 404, { error: 'room_missing' });
      if (match[2] === 'join' && req.method === 'POST') {
        const body = await json(req);
        if (body?.profile !== room.profile) return reply(res, 409, { error: 'different_profile' });
        // Check again after reading an asynchronous request body: only one guest can claim a room.
        if (rooms.get(match[1]) !== room || room.expiresAt <= now()) return reply(res, 404, { error: 'room_missing' });
        if (room.guest) return reply(res, 409, { error: 'room_full' });
        room.guest = token();
        return reply(res, 200, { code: match[1], participantToken: room.guest, expiresAt: room.expiresAt, iceServers });
      }
      const participant = req.headers['x-room-token'];
      const host = same(participant, room.host);
      if (!host && !(room.guest && same(participant, room.guest))) return reply(res, 403, { error: 'invalid_participant' });
      if (!match[2] && req.method === 'GET') return reply(res, 200, {
        peerPresent: Boolean(room.guest), expiresAt: room.expiresAt,
        remoteDescription: host ? room.answer : room.offer,
      });
      if (!match[2] && req.method === 'DELETE') { rooms.delete(match[1]); return reply(res, 200, { closed: true }); }
      if (match[2] === 'description' && req.method === 'PUT') {
        const body = await json(req);
        if (body?.type !== (host ? 'offer' : 'answer') || typeof body.sdp !== 'string' || body.sdp.length > 100_000 ||
            !/^m=application /m.test(body.sdp) || /^m=(audio|video) /m.test(body.sdp)) return reply(res, 400, { error: 'invalid_description' });
        const field = host ? 'offer' : 'answer';
        if (room[field] && room[field].sdp !== body.sdp) return reply(res, 409, { error: 'description_already_set' });
        room[field] = { type: body.type, sdp: body.sdp };
        return reply(res, 200, { accepted: true });
      }
      reply(res, 405, { error: 'method_not_allowed' });
    } catch (error) {
      if (!res.headersSent) reply(res, error.status ?? 500, { error: error.code ?? 'server_error' });
      else res.end();
    }
  });
  server.requestTimeout = 15_000;
  server.headersTimeout = 10_000;
  server.maxHeadersCount = 30;
  server.on('close', () => { clearInterval(timer); rooms.clear(); });
  return server;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const { ROOM_ACCESS_KEY, TURN_URL, TURN_USER, TURN_PASSWORD } = process.env;
  if (!/^turn:[^\s]+$/.test(TURN_URL ?? '') || /[?&]transport=(?!udp(?:&|$))/.test(TURN_URL ?? '') || !TURN_USER || !TURN_PASSWORD)
    throw new Error('Set TURN_URL, TURN_USER and TURN_PASSWORD.');
  const server = createRoomServer({ accessKey: ROOM_ACCESS_KEY, iceServers: [{ urls: TURN_URL, username: TURN_USER, credential: TURN_PASSWORD }] });
  server.listen(Number(process.env.PORT ?? 8080), '0.0.0.0', () => console.log('AetherBoy room service ready.'));
  for (const signal of ['SIGTERM', 'SIGINT']) process.on(signal, () => server.close());
}
