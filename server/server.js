
'use strict';
const http = require('http');
const crypto = require('crypto');
const WebSocket = require('ws');
const PORT = Number(process.env.PORT || 8080);
const rooms = new Map();
const sockets = new Set();
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

function code() { let s=''; for(let i=0;i<6;i++) s += alphabet[crypto.randomInt(alphabet.length)]; return s; }
function send(ws, data) { if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(data)); }
function roster(room) { return Array.from(room.players.values()).map(p => ({ playerId:p.id, playerName:p.name, isHost:p.id===room.hostId })); }
function roomState(room) { return { type:'room_state', roomCode:room.code, hostId:room.hostId, maxPlayers:room.maxPlayers, matchStarted:room.matchStarted, players:roster(room) }; }
function broadcast(room, data, except) { for (const p of room.players.values()) if (p.ws !== except) send(p.ws, data); }
function remove(ws) {
  const p = ws.player; if (!p) return;
  const room = rooms.get(p.roomCode); ws.player = null;
  if (!room) return;
  room.players.delete(p.id);
  if (p.id === room.hostId) {
    broadcast(room, {type:'room_closed', reason:'Host left the lobby'});
    for (const member of room.players.values()) { member.ws.player = null; send(member.ws, {type:'room_closed', reason:'Host left the lobby'}); }
    rooms.delete(room.code); return;
  }
  broadcast(room, {type:'peer_left', playerId:p.id});
  broadcast(room, roomState(room));
  if (room.players.size === 0) rooms.delete(room.code);
}
function joinRoom(ws, msg, create) {
  let room;
  if (create) {
    let id; do { id=code(); } while (rooms.has(id));
    room = { code:id, hostId:null, players:new Map(), maxPlayers:Math.max(2,Math.min(16,Number(msg.maxPlayers)||4)), matchStarted:false };
    rooms.set(id, room);
  } else room = rooms.get(String(msg.roomCode||'').toUpperCase());
  if (!room) return send(ws, {type:'error', error:'Room not found.'});
  if (room.matchStarted && !create) return send(ws, {type:'error', error:'The host has already started the match.'});
  if (room.players.size >= room.maxPlayers) return send(ws, {type:'error', error:'This lobby is full.'});
  const id = crypto.randomUUID();
  const player = { id, name:String(msg.playerName||'Farmer').slice(0,20), roomCode:room.code, ws };
  ws.player = player;
  if (!room.hostId) room.hostId = id;
  room.players.set(id, player);
  send(ws, { type:create?'created':'joined', roomCode:room.code, playerId:id, hostId:room.hostId, isHost:id===room.hostId, players:roster(room), maxPlayers:room.maxPlayers });
  broadcast(room, {type:'peer_joined', playerId:id, playerName:player.name, isHost:id===room.hostId}, ws);
  broadcast(room, roomState(room), ws);
}
const server = http.createServer((req,res) => {
  if (req.url === '/health' || req.url === '/') { res.writeHead(200, {'content-type':'application/json','access-control-allow-origin':'*'}); return res.end(JSON.stringify({ok:true, service:'pumpkin-panic-coop', rooms:rooms.size})); }
  res.writeHead(404); res.end('Not found');
});
const wss = new WebSocket.Server({ server, path:'/ws', maxPayload:16*1024 });
wss.on('connection', ws => {
  sockets.add(ws);
  ws.on('message', raw => {
    let msg; try { msg=JSON.parse(raw.toString()); } catch(e) { return send(ws,{type:'error',error:'Invalid JSON'}); }
    const p=ws.player;
    if (msg.type==='create') return p ? send(ws,{type:'error',error:'Already in a room.'}) : joinRoom(ws,msg,true);
    if (msg.type==='join') return p ? send(ws,{type:'error',error:'Already in a room.'}) : joinRoom(ws,msg,false);
    if (msg.type==='leave') return remove(ws);
    if (!p) return send(ws,{type:'error',error:'Join a room first.'});
    const room=rooms.get(p.roomCode); if (!room) return remove(ws);
    if (msg.type==='player_state') {
      const state={type:'player_state',playerId:p.id,x:finite(msg.x),y:finite(msg.y),z:finite(msg.z),facing:finite(msg.facing),moving:!!msg.moving,alive:msg.alive!==false};
      broadcast(room,state,ws); return;
    }
    if (msg.type==='input') { const host=room.players.get(room.hostId); if(host) send(host.ws,{type:'input',playerId:p.id,status:String(msg.status||'').slice(0,40),payload:String(msg.payload||'').slice(0,2048)}); return; }
    if (msg.type==='game_event') {
      if (p.id!==room.hostId) return send(ws,{type:'error',error:'Only the host can broadcast world events.'});
      return broadcast(room,{type:'game_event',playerId:p.id,status:String(msg.status||'').slice(0,40),payload:String(msg.payload||'').slice(0,2048)},ws);
    }
    if (msg.type==='start_match' || msg.type==='retry') {
      if (p.id!==room.hostId) return send(ws,{type:'error',error:'Only the host can do that.'});
      const scene=msg.scene==='Nightmare'?'Nightmare':'SampleScene';
      room.matchStarted=true;
      broadcast(room,{type:msg.type,scene,hostId:room.hostId});
      return;
    }
    if (msg.type==='pause') {
      if (p.id!==room.hostId) return send(ws,{type:'error',error:'Only the host can pause the match.'});
      return broadcast(room,{type:'pause',paused:!!msg.paused},ws);
    }
    send(ws,{type:'error',error:'Unknown message type.'});
  });
  ws.on('close',()=>{sockets.delete(ws);remove(ws);});
  ws.on('error',()=>{remove(ws);});
});
function finite(v) { const n=Number(v); return Number.isFinite(n)?Math.max(-100000,Math.min(100000,n)):0; }
const heartbeat=setInterval(()=>{ for(const ws of sockets) if(ws.readyState===WebSocket.OPEN) ws.ping(); },25000);
server.listen(PORT,'0.0.0.0',()=>console.log(`Pumpkin Panic co-op server listening on ${PORT}`));
process.on('SIGTERM',()=>{clearInterval(heartbeat);for(const ws of sockets)ws.close();server.close(()=>process.exit(0));});
