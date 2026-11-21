#!/usr/bin/env python3
"""Copy the checked-in WebGL co-op client overlay into the exported Unity project."""
import os
import shutil
import sys

source = os.path.join('ci', 'webgl-coop', 'Assets')
target_project = sys.argv[1] if len(sys.argv) > 1 else 'project'
target = os.path.join(target_project, 'Assets')
if not os.path.isdir(source):
    raise SystemExit('Missing co-op overlay: ' + source)
os.makedirs(target, exist_ok=True)
shutil.copytree(source, target, dirs_exist_ok=True)
print('Installed WebGL co-op assets into', target)
