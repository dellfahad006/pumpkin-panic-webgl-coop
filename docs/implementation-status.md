# Implementation status

## Implemented in source

- Photon PUN event transport preserving the original byte-array packet protocol.
- Steamworks compatibility types used by the decompiled mod: `SteamId`, `SteamClient`, `SteamNetworking`, `SteamMatchmaking`, `SteamFriends`, `Lobby`, and `Friend`.
- Receive queue buffering, queue bounds/overflow handling, 256-packet per-frame receive budget, reliable/unreliable mapping, and targeted/broadcast routing.
- Photon private rooms, 6-character room codes, 2–4 player capacity, nicknames, room metadata, Master Client ownership, player TTL/rejoin, and master-change pause behavior.
- BepInEx plugin lifecycle replaced with a persistent Unity runtime bootstrap; Harmony patch classes removed from the ported runtime.
- Direct hooks added to the original game scripts for player movement/death, enemy retargeting/attack/contact, crops, economy/workshop/key/motor purchases, gate input, boat repair/exit, pause behavior, tutorial readiness/skip, retry/menu/victory flow, deer/wendigo, shadow/candle, clown, pet triggers, shack zones, lantern input, and actor sorting.
- Browser visibility integration pauses the shared host simulation and backgrounded guests; PUN event dispatch is enabled while the simulation is paused.
- WebGL Steam test scripts are replaced by the existing no-op CI stubs and the Win64 Steamworks assembly is removed from the WebGL project.

## Not yet verified in Unity

- PUN 2.50 import and exact API compatibility of `PhotonRoomService`, `PhotonSteamTransport`, and the Steamworks shim.
- Compilation of the decompiled 12k-line `Plugin.cs` after conversion from BepInEx/Harmony and ILSpy output normalization.
- Runtime testing of all original mod packet handlers and all gameplay interactions.
- Four-player load, packet-rate measurements, latency/jitter, and queue sizing under real Photon Cloud conditions.
- Browser focus/background behavior in Chrome, Firefox, Safari and mobile browsers.
- Rejoin and Master Client migration under real disconnects. A mid-run Master Client change deliberately pauses and requires restart rather than claiming full state migration.

## Specific compatibility notes

- The old Harmony `Button.OnSubmit` patch is not installed. The port keeps the existing lobby-navigation selection clearing, which is the control path that patch protected. Verify gamepad/keyboard submit behavior in Unity; if a lobby control can still be submitted while navigation is locked, add a direct guard at that control's handler.
- Some decompiled ILSpy constructs were normalized (implicit operator calls, by-ref display casts, and `out` arguments). The Unity compiler is the authority on whether any remaining decompiler artifacts need cleanup.
- The room code UI is deliberately a minimal IMGUI front end. The original mod lobby UI remains in place for readiness and match controls; the code UI provides host/join/leave/copy actions.

## Required test sequence

1. Import PUN 2.50 and configure `PhotonServerSettings`.
2. Run the CI installer locally and compile in Unity 2020.3.40f1.
3. Test two browser tabs: host room, join by code, player spawn/animation, pause/resume, death/revive, crop/farm, fishing, workshop/economy, enemies, gate/boat, retry, victory.
4. Repeat with four players and inspect Photon traffic/room message rate.
5. Test host tab hidden, guest tab hidden, network loss under 120 seconds, host disconnect, guest disconnect, and rejoin.
6. Only after these pass, deploy the WebGL build to GitHub Pages.

## Network traffic measurement

The room overlay reports actual transport packet and byte rates (TX/RX per second). The source audit found these existing subsystem cadences before transport conversion: player/deer/shadow snapshots about 20 Hz, enemy snapshots about 10 Hz, match-event snapshots about 6.7 Hz, economy snapshots about 4 Hz, and farm updates in batches of three tiles every 0.5 seconds. These are subsystem send cadences, not a measured aggregate room event rate; targeted fan-out to multiple peers increases the actual event count and must be measured in a live room.
