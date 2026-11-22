#!/usr/bin/env python3
"""Apply the PUN co-op port bundle to a checkout of pumpkin-panic-webgl-coop."""
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parent
WORKFLOW = ROOT / ".github" / "workflows" / "webgl.yml"
OLD_STEP = """      - name: Install WebGL co-op client files
        run: python3 ci/install_webgl_coop.py project"""
NEW_STEP = """      - name: Install Photon PUN 2.50 co-op port
        env:
          PUN2_UNITYPACKAGE_URL: ${{ secrets.PUN2_UNITYPACKAGE_URL }}
          PUN2_UNITYPACKAGE_TOKEN: ${{ secrets.PUN2_UNITYPACKAGE_TOKEN }}
          PHOTON_APP_ID: ${{ secrets.PHOTON_APP_ID }}
        run: python3 ci/install_pun_coop.py project"""


def main() -> int:
    if not WORKFLOW.exists():
        print("Run this script from the root of pumpkin-panic-webgl-coop (after extracting the bundle).", file=sys.stderr)
        return 2
    content = WORKFLOW.read_text(encoding="utf-8")
    if OLD_STEP in content:
        WORKFLOW.write_text(content.replace(OLD_STEP, NEW_STEP, 1), encoding="utf-8")
        print("Updated WebGL workflow to install the PUN 2.50 port")
    elif "python3 ci/install_pun_coop.py project" in content:
        print("WebGL workflow already uses the PUN co-op installer")
    else:
        print("Could not find the existing co-op install step in the workflow", file=sys.stderr)
        return 1

    shutil.rmtree(ROOT / "ci" / "webgl-coop", ignore_errors=True)
    shutil.rmtree(ROOT / "server", ignore_errors=True)
    for path in (ROOT / "ci" / "install_webgl_coop.py", ROOT / "render.yaml"):
        if path.exists():
            path.unlink()

    required = [
        ROOT / "ci" / "install_pun_coop.py",
        ROOT / "ci" / "pun-coop" / "game-scripts.patch",
        ROOT / "ci" / "pun-coop" / "Assets" / "Scripts" / "PumpkinPanicCoop" / "Plugin.cs",
    ]
    missing = [str(path.relative_to(ROOT)) for path in required if not path.exists()]
    if missing:
        print("Bundle is incomplete; missing: " + ", ".join(missing), file=sys.stderr)
        return 1

    print("PUN port applied. Review `git diff` before committing.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
