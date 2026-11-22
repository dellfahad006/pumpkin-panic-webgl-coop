# PUN co-op source payload

This directory is copied into the recovered Unity project by `ci/install_pun_coop.py`.

- `Assets/Scripts/PumpkinPanicCoop/` contains the ported mod/runtime and Photon transport.
- `Assets/Plugins/WebGL/PumpkinPanicVisibility.jslib` bridges browser visibility changes to Unity.
- `game-scripts.patch` applies the direct hooks to the original game's `Assembly-CSharp` scripts after `ci/patch_game.py` has run.

Photon PUN 2.50 is intentionally not included here. See the repository README for private package and App ID setup.
