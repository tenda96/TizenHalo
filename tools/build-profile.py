#!/usr/bin/env python3
"""Build explicit capture/runtime profiles without editing user config or the tracked manifest."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROFILES = ("q74t-55", "rgb-55", "s84f-90", "ru7100-50")
NS = "http://tizen.org/ns/packages"
ET.register_namespace("", NS)


def load_profile(name):
    if name not in PROFILES:
        raise ValueError("Unknown profile: " + name)
    data = json.loads((ROOT / "compatibility/profiles" / (name + ".json")).read_text())
    expected = {
        "q74t-55": ("tizen55", "5.5", "video_plane", "q74t-55-native-v1", "screen_video", False),
        "rgb-55": ("tizen55", "5.5", "ppi", "none", "main", True),
        "s84f-90": ("tizen90", "9.0", "video_plane", "s84f-90-native-v1", "main", True),
        "ru7100-50": ("tizen50", "5.0", "file", "none", "main", True),
    }[name]
    assert data["id"] == name
    assert tuple(data[k] for k in ("target_framework", "api_version", "capture_backend", "video_abi", "default_method", "experimental")) == expected
    assert type(data["default_capture_fps"]) is int and 1 <= data["default_capture_fps"] <= 30
    return data


def make_manifest(profile):
    tree = ET.parse(ROOT / "app/tizen-manifest.xml")
    manifest = tree.getroot()
    for element in (manifest, manifest.find(f"{{{NS}}}ui-application"), manifest.find(f"{{{NS}}}service-application")):
        element.set("api-version", profile["api_version"])
    if profile["experimental"]:
        ui = manifest.find(f"{{{NS}}}ui-application/{{{NS}}}label")
        labels = {"rgb-55": "RGB", "s84f-90": "S84F", "ru7100-50": "RU7100"}
        ui.text = "TizenHalo " + labels.get(profile["id"], profile["id"]) + " (test)"
    path = ROOT / "dist/.profiles" / profile["id"] / "tizen-manifest.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    tree.write(path, encoding="utf-8", xml_declaration=True)
    return path, manifest.get("version")


def build(name):
    profile = load_profile(name)
    manifest, version = make_manifest(profile)
    env = os.environ.copy()
    env.setdefault("DOTNET_CLI_HOME", str(ROOT / ".dotnet-home"))
    env.setdefault("NUGET_PACKAGES", str(ROOT / ".nuget"))
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    subprocess.run(["dotnet", "build", "app/TizenHalo.csproj", "-c", "Release", "--nologo",
                    "-p:NuGetAudit=false", "-p:UseSharedCompilation=false", "-p:HaloProfile=" + name,
                    "-p:HaloGeneratedManifest=" + str(manifest)], cwd=ROOT, env=env, check=True)
    built = ROOT / "app/bin/Release" / profile["target_framework"] / ("io.github.tenda96.TizenHalo-" + version + ".tpk")
    output = ROOT / "dist" / ("TizenHalo-" + version + "-" + name + ".tpk")
    subprocess.run([sys.executable, "tools/verify-package.py", str(built), "--profile", name], cwd=ROOT, check=True)
    # Never replace a distributable with a package that failed profile validation.
    shutil.copyfile(built, output)
    if name == "q74t-55":
        shutil.copyfile(output, ROOT / "dist" / ("TizenHalo-" + version + ".tpk"))
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("profile", choices=PROFILES + ("all",), nargs="?", default="q74t-55")
    args = parser.parse_args()
    outputs = [build(name) for name in (PROFILES if args.profile == "all" else (args.profile,))]
    print("Built profiles:\n" + "\n".join(str(p) for p in outputs))


if __name__ == "__main__":
    main()
