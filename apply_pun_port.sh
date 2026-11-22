#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

if [ ! -f .github/workflows/webgl.yml ]; then
  echo "Run this script from the pumpkin-panic-webgl-coop repository root." >&2
  exit 1
fi

python3 - <<'PY'
from pathlib import Path
p = Path('.github/workflows/webgl.yml')
s = p.read_text(encoding='utf-8')
old = '''      - name: Install WebGL co-op client files
        run: python3 ci/install_webgl_coop.py project'''
new = '''      - name: Install Photon PUN 2.50 co-op port
        env:
          PUN2_UNITYPACKAGE_URL: ${{ secrets.PUN2_UNITYPACKAGE_URL }}
          PUN2_UNITYPACKAGE_TOKEN: ${{ secrets.PUN2_UNITYPACKAGE_TOKEN }}
          PHOTON_APP_ID: ${{ secrets.PHOTON_APP_ID }}
        run: python3 ci/install_pun_coop.py project'''
if old in s:
    p.write_text(s.replace(old, new, 1), encoding='utf-8')
elif 'python3 ci/install_pun_coop.py project' in s:
    print('Workflow already points to the PUN co-op installer')
else:
    raise SystemExit('Could not find the old WebGL co-op install step in .github/workflows/webgl.yml')
PY

rm -rf ci/webgl-coop server
rm -f ci/install_webgl_coop.py render.yaml

printf '\nPUN co-op port applied. Review the diff before committing:\n'
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git status --short
else
  echo "(not inside a Git worktree; no status available)"
fi
