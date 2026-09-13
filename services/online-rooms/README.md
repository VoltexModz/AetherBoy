# AetherBoy private room service

Dependency-free Node.js 24 service for two-player, short-code signaling.
Deployment and emulator setup: [German guide](../../docs/ONLINE_ROOMS_DE.md).

Required environment: `ROOM_ACCESS_KEY` (32–256 random ASCII characters),
`TURN_URL`, `TURN_USER`, `TURN_PASSWORD`. `PORT` defaults to 8080.
Use HTTPS termination, one replica, no persistent volume. No browser CORS is
needed: native clients contact the service. Do not cache authenticated responses.

All room routes require `Authorization: Bearer <ROOM_ACCESS_KEY>`.
Create/join returns a private `participantToken`; subsequent room polling,
description upload and deletion also require `X-Room-Token`. Codes expire in
10 minutes; capacity is 100 rooms; creation/join admission is globally limited
to 60 requests/minute. SDP is bounded and stored only in memory. The service
never receives ROMs, battery saves or emulator packets. Authenticated users are
trusted private-group members and receive configured relay credentials.

Run tests: `node --test server.test.mjs`.
