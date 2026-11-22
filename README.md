# Pumpkin Panic WebGL Co-op — Photon PUN port

This replaces the abandoned Node/WebSocket prototype with a source port of the decompiled Steam co-op mod. The target game remains Unity **2020.3.40f1** and the target party size is **2–4 players**.

## Architecture

- Photon PUN 2.50 is used as a hosted room/event relay. No Node relay server or Render service is needed.
- The original mod's binary packet protocol is retained: its first-byte opcodes and payload offsets are not rewritten as JSON or Photon RPCs.
- `SteamworksCompat.cs` supplies the subset of Facepunch Steamworks used by the mod. Photon ActorNumbers are represented as 64-bit values in legacy packet fields; lobby IDs are stable room-code hashes.
- `PhotonSteamTransport.cs` maps targeted/broadcast P2P sends to `PhotonNetwork.RaiseEvent`, and queues Photon event callbacks so the original receive-polling loop can continue to run.
- BepInEx and Harmony are removed from the runtime. The ported plugin is a persistent game-owned `MonoBehaviour`; Harmony behavior is moved into direct hooks in the original game scripts.
- A six-character private room code replaces Steam invitations. Photon room capacity is capped at four.

## One-time Photon setup

The PUN SDK is **not vendored in this public repository**. It is a licensed Unity Asset Store package.

1. Obtain the **Photon PUN 2.50** Unity package. This version is the compatibility target for the recovered Unity 2020.3.40f1 project; do not substitute a newer PUN release without testing its minimum Editor version and API changes.
2. Store the `.unitypackage` in private HTTPS storage or a private GitHub release. The GitHub Actions runner must be able to download it.
3. Run the PUN Setup Wizard once in a Unity 2020.3 project with PUN 2.50. Export the PUN SDK assets plus the generated `PhotonServerSettings.asset` as a `.unitypackage`, then store that package in the private location. Leave the App ID field as a placeholder; CI injects the real ID.
4. Add these GitHub Actions repository secrets:
   - `PUN2_UNITYPACKAGE_URL`: private HTTPS download URL for the PUN 2.50 `.unitypackage`.
   - `PUN2_UNITYPACKAGE_TOKEN`: optional bearer token if the download URL requires authentication.
   - `PHOTON_APP_ID`: Photon Realtime App ID from the Photon Dashboard.
5. The workflow downloads/imports the licensed package, verifies PUN 2.50, injects `PHOTON_APP_ID` into `PhotonServerSettings.asset`, then installs the co-op source port.

The game archive itself does not need to be republished with Photon binaries. The PUN package stays in private storage and is fetched by CI.

## Apply to the repository

From the repository root, extract this port bundle over the repo and run:

```bash
python apply_pun_port.py
```

On Linux/macOS, `bash apply_pun_port.sh` does the same thing.

The script updates the WebGL workflow, removes the abandoned `ci/webgl-coop`/Node relay prototype, and preserves the existing game-recovery/performance pipeline. It does not commit or push anything.

## Local installation

With a Unity project folder named `project/` and PUN 2.50 already imported:

```bash
PHOTON_APP_ID='your-photon-realtime-app-id' python3 ci/install_pun_coop.py project
```

If PUN is not already imported, also set `PUN2_UNITYPACKAGE_URL` (and optionally `PUN2_UNITYPACKAGE_TOKEN`).

## Runtime behavior

- Host = Photon Master Client. If the Master Client changes during a run, the run is paused and the UI asks players to restart; this port does not pretend to perform a full authoritative world-state migration.
- Room players use Photon ActorNumbers. They are room-local and are not permanent Steam IDs; only their 8-byte packet representation is preserved.
- The receive queue is bounded (2,048 packets), and at most 256 packets are drained per Unity frame. Overflow causes the client to leave the room rather than continue with dropped state packets.
- Snapshot send rates remain those of the original mod; the port does not add per-frame Photon transform replication. The room overlay reports TX/RX packets and bytes per second so actual room traffic can be measured.
- Browser visibility changes pause the host's shared simulation and locally pause backgrounded guests. PUN callback dispatch is configured to continue when `Time.timeScale` is zero. Browser throttling still exists; this is not a guarantee that a hidden tab continues to simulate at full frame rate.
- PUN uses WebSocket Secure for WebGL. No separate WebSocket server is deployed.

## Port status and validation boundary

This bundle contains the transport, room lifecycle, Steamworks compatibility surface, plugin lifecycle conversion, and direct game-script hooks. It also removes the abandoned prototype. It has been source-audited and the generated game-script patch has been dry-run against the recovered Unity scripts, including the existing `openDoor.cs` recovery patch.

**It has not been compiled in Unity or run as a WebGL build in this environment** because the Unity Editor and licensed PUN 2.50 SDK are not available here. The first Unity import/build is therefore a required validation step, not a claimed success. Fix any compiler/API issues against the exact PUN 2.50 package before publishing a playable build.

See [`docs/implementation-status.md`](docs/implementation-status.md) for the direct-hook coverage and remaining test checklist.
