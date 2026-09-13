// Started only by OnlineRoomTests; no production credentials or external listener.
import { createRoomServer } from '../../services/online-rooms/server.mjs';
const iceServers = JSON.parse(process.env.AETHERBOY_ROOM_TEST_ICE_SERVERS ?? '[]');
const server = createRoomServer({ accessKey: 'a'.repeat(32), iceServers });
server.listen(0, '127.0.0.1', () => console.log(server.address().port));
