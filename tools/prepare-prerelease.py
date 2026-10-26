#!/usr/bin/env python3
"""Prepare verified, allowlisted public-test assets locally. Does not push or publish."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROFILES = ("q74t-55", "rgb-55", "s84f-90")


def release_tag(package_version, runtime_version):
    match = re.fullmatch(r"tizenhalo-(\d+\.\d+\.\d+)-(alpha|beta|rc)\.\d+", runtime_version)
    if not match or match.group(1) != package_version:
        raise ValueError("Numeric package version and prerelease runtime identity must match")
    return "v" + runtime_version[len("tizenhalo-"):]


def main():
    version = ET.parse(ROOT / "app/tizen-manifest.xml").getroot().get("version")
    source = (ROOT / "app/src/ServiceHeartbeat.cs").read_text(encoding="utf-8")
    identity = re.search(r'public const string Version = "([^"]+)";', source).group(1)
    tag = release_tag(version, identity)
    git = lambda *args: subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()
    if git("status", "--porcelain"):
        raise SystemExit("Commit the reviewed source/checkpoint before preparing public assets")
    commit = git("rev-parse", "HEAD")
    output = ROOT / "dist" / tag
    if output.exists():
        raise SystemExit("Refusing to replace an existing test kit: " + str(output))

    factory = json.loads((ROOT / "app/config.example.json").read_text())
    packages = []
    for name in PROFILES:
        path = ROOT / "dist" / ("TizenHalo-" + version + "-" + name + ".tpk")
        subprocess.run([sys.executable, "tools/verify-package.py", str(path), "--profile", name], cwd=ROOT, check=True)
        with zipfile.ZipFile(path) as archive:
            if json.loads(archive.read("res/config.json")) != factory:
                raise SystemExit("Public test packages must use factory config, not personal settings")
            if identity.encode("utf-16le") not in archive.read("bin/TizenHalo.dll"):
                raise SystemExit("Compiled runtime identity does not match the proposed prerelease")
        packages.append(path)
    probe = ROOT / "dist/TizenHalo-Compatibility-Probe-0.1.0-tizen50.tpk"
    subprocess.run([sys.executable, "tools/compatibility-probe/verify-package.py", str(probe)], cwd=ROOT, check=True)
    packages.append(probe)

    notes = ROOT / "docs/releases" / (tag + ".md")
    with tempfile.TemporaryDirectory(prefix=".prepare-", dir=ROOT / "dist") as temp:
        stage = Path(temp) / "assets"
        stage.mkdir()
        for path in packages:
            shutil.copyfile(path, stage / path.name)
        shutil.copyfile(notes, stage / "RELEASE-NOTES.md")
        shutil.copyfile(ROOT / "docs/TESTING.md", stage / "TESTING.md")
        shutil.copyfile(ROOT / "LICENSE", stage / "LICENSE")
        (stage / "SOURCE.json").write_text(json.dumps({
            "tag": tag, "package_version": version, "runtime_version": identity,
            "source_commit": commit, "source_url": "https://github.com/tenda96/TizenHalo/tree/" + commit,
            "prerelease": True, "new_revision_tested_on_device": False,
            "profiles": list(PROFILES), "diagnostic_only": probe.name,
        }, indent=2) + "\n")
        checksums = "".join(hashlib.sha256(p.read_bytes()).hexdigest() + "  " + p.name + "\n"
                            for p in sorted(stage.iterdir()))
        (stage / "SHA256SUMS").write_text(checksums)
        bundle = stage / ("TizenHalo-" + tag + "-test-kit.zip")
        members = sorted(p for p in stage.iterdir() if p != bundle)
        with zipfile.ZipFile(bundle, "w", zipfile.ZIP_DEFLATED) as archive:
            for path in members:
                archive.write(path, path.name)
        with zipfile.ZipFile(bundle) as archive:
            assert archive.testzip() is None
            assert set(archive.namelist()) == {p.name for p in members}
        shutil.move(str(stage), str(output))
    print(json.dumps({"local_assets": str(output), "source_commit": commit, "tag": tag,
                      "zip_bytes": (output / bundle.name).stat().st_size, "published": False}, indent=2))


if __name__ == "__main__":
    main()
