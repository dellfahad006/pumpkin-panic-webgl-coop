#!/usr/bin/env python3
"""Install the source-port of Pumpkin Panic co-op into the exported Unity project.

PUN is a licensed Unity Asset Store package and is deliberately not vendored here.
Provide PUN2_UNITYPACKAGE_URL (and optionally PUN2_UNITYPACKAGE_TOKEN), or include
PUN 2.50 in the project archive before running this installer.
"""
from __future__ import annotations

import io
import os
import re
import shutil
import subprocess
import sys
import tarfile
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath

REPO_ROOT = Path(__file__).resolve().parent.parent
PORT_ROOT = REPO_ROOT / "ci" / "pun-coop"
SUPPORTED_PUN_VERSIONS = {"2.50", "2.56"}


def log(message: str) -> None:
    print(f"[pun-coop] {message}", flush=True)


def safe_project_path(project: Path, relative: str) -> Path:
    rel = PurePosixPath(relative)
    if rel.is_absolute() or ".." in rel.parts:
        raise RuntimeError(f"Unsafe path inside Unity package: {relative}")
    target = (project / Path(*rel.parts)).resolve()
    if project.resolve() not in target.parents and target != project.resolve():
        raise RuntimeError(f"Unity package path escapes project: {relative}")
    return target


def import_unitypackage(package_bytes: bytes, project: Path) -> None:
    """Import a .unitypackage archive without launching the Unity Editor."""
    imported = 0
    try:
        archive = tarfile.open(fileobj=io.BytesIO(package_bytes), mode="r:gz")
    except tarfile.TarError:
        # Some private artifact hosts repackage the .unitypackage as a zip.
        archive = None

    if archive is not None:
        with archive:
            members = {m.name: m for m in archive.getmembers() if m.isfile()}
            folders = sorted({name.rsplit("/", 1)[0] for name in members if name.endswith("/pathname")})
            for folder in folders:
                pathname_member = members.get(folder + "/pathname")
                if pathname_member is None:
                    continue
                raw_path = archive.extractfile(pathname_member).read().decode("utf-8", "replace").splitlines()[0].strip()
                if not raw_path:
                    continue
                destination = safe_project_path(project, raw_path)
                destination.parent.mkdir(parents=True, exist_ok=True)
                asset_member = members.get(folder + "/asset")
                meta_member = members.get(folder + "/asset.meta")
                if raw_path.endswith("/"):
                    destination.mkdir(parents=True, exist_ok=True)
                elif asset_member is not None:
                    with archive.extractfile(asset_member) as src, destination.open("wb") as dst:
                        shutil.copyfileobj(src, dst)
                    imported += 1
                if meta_member is not None:
                    with archive.extractfile(meta_member) as src, (Path(str(destination) + ".meta")).open("wb") as dst:
                        shutil.copyfileobj(src, dst)
        log(f"Imported {imported} Unity package assets from PUN SDK")
        return

    with zipfile.ZipFile(io.BytesIO(package_bytes)) as archive:
        # A normal .unitypackage is a tar.gz, but accept a pre-expanded zip artifact too.
        for name in archive.namelist():
            if name.endswith("/"):
                continue
            target = safe_project_path(project, name)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(name))
            imported += 1
    log(f"Imported {imported} zipped PUN assets")


def download_pun_package(project: Path) -> None:
    url = os.environ.get("PUN2_UNITYPACKAGE_URL", "").strip()
    if not url:
        raise RuntimeError(
            "Photon PUN 2.50 is not present in the exported Unity project. "
            "Add the licensed PUN 2.50 .unitypackage to private storage and set the "
            "GitHub Actions secret PUN2_UNITYPACKAGE_URL (plus PUN2_UNITYPACKAGE_TOKEN "
            "if authentication is required). Do not commit the licensed SDK to this public repo."
        )
    headers = {"User-Agent": "pumpkin-panic-pun-coop-ci", "Accept": "application/octet-stream"}
    token = os.environ.get("PUN2_UNITYPACKAGE_TOKEN", "").strip()
    if token:
        headers["Authorization"] = "Bearer " + token
    request = urllib.request.Request(url, headers=headers)
    log("Downloading the privately hosted PUN SDK artifact")
    with urllib.request.urlopen(request, timeout=120) as response:
        package_bytes = response.read()
    if not package_bytes:
        raise RuntimeError("The PUN SDK download returned an empty file")
    import_unitypackage(package_bytes, project)
    for nested_pkg in sorted((project / "Assets").rglob("*.unitypackage")):
        log(f"Unpacking nested Unity package: {nested_pkg.relative_to(project)}")
        import_unitypackage(nested_pkg.read_bytes(), project)
        nested_pkg.unlink()


def find_pun_network_file(project: Path) -> Path | None:
    candidates = list((project / "Assets").rglob("PhotonNetwork.cs"))
    return candidates[0] if candidates else None


def verify_pun_version(project: Path) -> None:
    source = find_pun_network_file(project)
    if source is None:
        raise RuntimeError("PUN import did not provide PhotonNetwork.cs under Assets/")
    text = source.read_text(encoding="utf-8", errors="replace")
    match = re.search(r'PunVersion\s*=\s*["\']([^"\']+)', text)
    if not match:
        # PUN's version constant has moved between source files in some releases.
        version_files = list((project / "Assets").rglob("PunVersion.cs"))
        for version_file in version_files:
            version_text = version_file.read_text(encoding="utf-8", errors="replace")
            match = re.search(r'["\'](\d+\.\d+)\.["\']?', version_text)
            if match:
                break
    if match and match.group(1) not in SUPPORTED_PUN_VERSIONS:
        raise RuntimeError(
            f"This port is pinned to Photon PUN {sorted(SUPPORTED_PUN_VERSIONS)}; found {match.group(1)} at {source}"
        )
    if not match:
        raise RuntimeError(
            f"Could not verify the PUN version from {source}. Install the PUN {sorted(SUPPORTED_PUN_VERSIONS)} SDK."
        )
    log(f"Verified Photon PUN {match.group(1)}")


def configure_photon_app_id(project_root):
    """Validate the App ID; Unity creates PhotonServerSettings after PUN import."""
    app_id = os.environ.get("PHOTON_APP_ID", "").strip()
    if not app_id:
        raise RuntimeError("PHOTON_APP_ID secret is required")
    settings = Path(project_root) / "Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset"
    if settings.is_file():
        # If a settings asset was supplied in the SDK bundle, update it in place.
        text = settings.read_text(encoding="utf-8", errors="replace")
        if "AppIdRealtime:" in text:
            text = re.sub(r"(?m)^(\s*AppIdRealtime:\s*).*?$", lambda x: x.group(1) + app_id, text, count=1)
            settings.write_text(text, encoding="utf-8")
        else:
            print("PhotonServerSettings.asset exists; AppIdRealtime will be set by the Unity Editor CI step")
    else:
        print("PhotonServerSettings.asset will be generated by the Unity Editor CI step")


def install_port(project: Path) -> None:
    # Apply targeted game-script diffs after ci/patch_game.py, preserving its performance fixes.
    marker = project / "Assets" / "Scripts" / "PumpkinPanicCoop" / "Plugin.cs"
    patch_file = PORT_ROOT / "game-scripts.patch"
    if not marker.exists():
        command = [
            "patch", "-p1", "--forward", "--batch", "--no-backup-if-mismatch",
            "-d", str(project), "-i", str(patch_file)
        ]
        result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if result.returncode != 0:
            print(result.stdout)
            raise RuntimeError("Could not apply the co-op game-script hooks")
        log("Applied direct co-op hooks to the original game scripts")
    else:
        log("Game-script hooks already present; skipping patch")

    assets = PORT_ROOT / "Assets"
    for relative in (Path("Scripts/PumpkinPanicCoop"), Path("Plugins/WebGL/PumpkinPanicVisibility.jslib"), Path("Plugins/WebGL/PumpkinPanicForensics.jslib")):
        source = assets / relative
        destination = project / "Assets" / relative
        if source.is_dir():
            if destination.exists():
                shutil.rmtree(destination)
            shutil.copytree(source, destination)
            source_meta = Path(str(source) + ".meta")
            if source_meta.exists():
                shutil.copy2(source_meta, Path(str(destination) + ".meta"))
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            source_meta = Path(str(source) + ".meta")
            if source_meta.exists():
                shutil.copy2(source_meta, Path(str(destination) + ".meta"))

    # Preserve the no-Steam WebGL stubs that the old build workflow already used.
    for name in ("SteamTest.cs", "AchievementTest.cs"):
        source = REPO_ROOT / "ci" / name
        matches = list((project / "Assets").rglob(name))
        if not matches:
            raise RuntimeError(f"Could not locate {name} in the exported Unity project")
        for destination in matches:
            shutil.copy2(source, destination)

    # Remove the abandoned JSON/WebSocket prototype from older exported project archives.
    shutil.rmtree(project / "Assets" / "Scripts" / "CoopWebGL", ignore_errors=True)
    old_jslib = project / "Assets" / "Plugins" / "WebGL" / "PumpkinPanicCoop.jslib"
    if old_jslib.exists():
        old_jslib.unlink()
    old_meta = Path(str(old_jslib) + ".meta")
    if old_meta.exists():
        old_meta.unlink()

    # Steam is not part of the WebGL player; the port supplies a tiny source-level shim.
    for path in (project / "Assets").rglob("Facepunch.Steamworks*"):
        if path.is_file():
            path.unlink()
    for path in (project / "Assets").rglob("steam_api*"):
        if path.is_file():
            path.unlink()

    log("Installed PumpkinPanicCoop PUN port sources")


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: install_pun_coop.py <unity-project-directory>", file=sys.stderr)
        return 2
    project = Path(sys.argv[1]).resolve()
    if not (project / "Assets").is_dir():
        raise RuntimeError(f"Unity project Assets folder not found: {project / 'Assets'}")

    if find_pun_network_file(project) is None:
        download_pun_package(project)
    verify_pun_version(project)
    configure_photon_app_id(project)
    install_port(project)
    editor_source = PORT_ROOT / "Assets/Editor/GeneratePhotonServerSettings.cs"
    editor_target = project / "Assets/Editor/GeneratePhotonServerSettings.cs"
    editor_target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(editor_source, editor_target)
    log("PUN co-op installation complete")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
