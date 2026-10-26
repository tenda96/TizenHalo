#!/usr/bin/env python3
"""Offline checks of a built TizenHalo package: manifest, bundled files and signed file digests.

Usage: python3 tools/verify-package.py [path/to/package.tpk]
Without an argument it checks dist/TizenHalo-<manifest version>.tpk.
"""
import base64
import hashlib
import json
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from urllib.parse import unquote

NS = {"t": "http://tizen.org/ns/packages"}
DSIG = "{http://www.w3.org/2000/09/xmldsig#}"
PACKAGE_ID = "io.github.tenda96.TizenHalo"

root = Path(__file__).resolve().parent.parent
version = ET.parse(root / "app/tizen-manifest.xml").getroot().attrib["version"]
package = Path(sys.argv[1]) if len(sys.argv) > 1 else root / f"dist/TizenHalo-{version}.tpk"

with zipfile.ZipFile(package) as z:
    names = set(z.namelist())
    manifest = ET.fromstring(z.read("tizen-manifest.xml"))
    ui = manifest.find("t:ui-application", NS)
    service = manifest.find("t:service-application", NS)
    assert manifest.attrib["version"] == version
    assert manifest.attrib["api-version"] == ui.attrib["api-version"] == service.attrib["api-version"] == "5.5"
    assert manifest.attrib["package"] == ui.attrib["appid"] == PACKAGE_ID
    assert service.attrib["appid"] == PACKAGE_ID + ".Service"
    assert ui.attrib["exec"] == service.attrib["exec"] == "TizenHalo.dll"
    assert ui.attrib["nodisplay"] == "false" and service.attrib["nodisplay"] == "true"
    privileges = [p.text for p in manifest.findall("t:privileges/t:privilege", NS)]
    assert "http://tizen.org/privilege/appmanager.launch" in privileges
    assert service.find("t:background-category", NS).attrib["value"] == "background-network"

    config_source = root / "app/config.json"
    if not config_source.exists():
        config_source = root / "app/config.example.json"
    assert json.loads(z.read("res/config.json")) == json.loads(config_source.read_text())

    deps = json.loads(z.read("TizenHalo.deps.json"))
    assert deps["runtimeTarget"]["name"].startswith("Tizen,Version=v5.5")
    assert "Net.Codecrete.QrCodeGenerator/2.0.4" in deps["libraries"]
    assert z.read("bin/TizenHalo.dll")[:2] == b"MZ"
    assert z.read("bin/QrCodeGenerator.dll")[:2] == b"MZ"
    assert not any(n.endswith("Launcher.dll") for n in names), "launcher must be compiled into the app"
    assert z.read("shared/res/TizenHalo.png")[:8] == b"\x89PNG\r\n\x1a\n"
    assert b"/api/settings" in z.read("res/Settings.html")
    assert b"captureMethod" in z.read("res/Settings.html")
    assert b"/api/layout" in z.read("res/Layout.html")
    assert b"MIT License" in z.read("res/THIRD-PARTY.txt")
    assert b"MIT License" in z.read("res/LICENSE.txt")

    checked = 0
    for signature in ["author-signature.xml", "signature1.xml"]:
        sig = ET.fromstring(z.read(signature))
        for ref in sig.findall(f".//{DSIG}Reference"):
            uri = ref.attrib.get("URI", "")
            if not uri or uri.startswith("#"):
                continue
            algorithm = ref.find(f"{DSIG}DigestMethod").attrib["Algorithm"]
            hash_name = algorithm.rsplit("#", 1)[-1]
            assert hash_name in ("sha256", "sha512"), algorithm
            expected = ref.findtext(f"{DSIG}DigestValue").strip()
            actual = base64.b64encode(hashlib.new(hash_name, z.read(unquote(uri))).digest()).decode()
            assert expected == actual, (signature, uri)
            checked += 1
    assert checked >= 6

print(json.dumps({
    "package": package.name,
    "version": version,
    "sha256": hashlib.sha256(package.read_bytes()).hexdigest(),
    "signed_file_digests_checked": checked,
    "checks": "PASS",
}, indent=2))
