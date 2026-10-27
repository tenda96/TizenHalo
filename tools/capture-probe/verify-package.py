#!/usr/bin/env python3
"""Verify the separate experimental capture-probe package, including signature digests."""
import base64
import hashlib
import json
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from urllib.parse import unquote

root = Path(__file__).resolve().parent
path = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'bin/Release/tizen50/io.github.tenda96.TizenHalo.CaptureProbe-0.2.0.tpk'
ns = {'t': 'http://tizen.org/ns/packages'}
ds = '{http://www.w3.org/2000/09/xmldsig#}'
with zipfile.ZipFile(path) as z:
    manifest = ET.fromstring(z.read('tizen-manifest.xml'))
    ui = manifest.find('t:ui-application', ns)
    assert manifest.attrib['package'] == ui.attrib['appid'] == 'io.github.tenda96.TizenHalo.CaptureProbe'
    assert manifest.attrib['api-version'] == ui.attrib['api-version'] == '5.0'
    assert ui.attrib['exec'] == 'TizenHaloCaptureProbe.dll'
    assert manifest.find('t:service-application', ns) is None
    assert sorted(x.text for x in manifest.findall('t:privileges/t:privilege', ns)) == [
        'http://tizen.org/privilege/internet', 'http://tizen.org/privilege/network.get']
    deps = json.loads(z.read('TizenHaloCaptureProbe.deps.json'))
    assert deps['runtimeTarget']['name'].startswith('Tizen,Version=v5.0')
    assert z.read('bin/TizenHaloCaptureProbe.dll')[:2] == b'MZ'
    assert z.read('shared/res/TizenHalo.png')[:8] == b'\x89PNG\r\n\x1a\n'
    assert b'MIT License' in z.read('res/LICENSE.txt')
    checked = 0
    for name in ('author-signature.xml', 'signature1.xml'):
        for ref in ET.fromstring(z.read(name)).findall('.//' + ds + 'Reference'):
            uri = ref.attrib.get('URI', '')
            if not uri or uri.startswith('#'):
                continue
            algorithm = ref.find(ds + 'DigestMethod').attrib['Algorithm'].rsplit('#', 1)[-1]
            assert algorithm in ('sha256', 'sha512')
            actual = base64.b64encode(hashlib.new(algorithm, z.read(unquote(uri))).digest()).decode()
            assert actual == ref.findtext(ds + 'DigestValue').strip(), uri
            checked += 1
    assert checked >= 6
print(json.dumps({'package': str(path), 'bytes': path.stat().st_size,
    'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
    'signed_file_digests_checked': checked, 'checks': 'PASS'}, indent=2))
