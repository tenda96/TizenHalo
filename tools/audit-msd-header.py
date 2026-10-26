#!/usr/bin/env python3
"""Read an official Samsung ZIP's MSDU11 headers and test a supplied public key catalogue.

Does not install firmware, contact a TV, extract partitions, or print key bytes.
Format reference: https://github.com/theubusu/unixtract/tree/main/src/formats/msd11
"""
import argparse
import hashlib
import json
import re
import struct
import subprocess
import zipfile
from pathlib import Path


def audit(firmware, catalogue):
    text = catalogue.read_text().split('collection "MSD11": {', 1)[1].split('\n}', 1)[0]
    candidates = re.findall(r'"([^\"]+)"\s*:\s*\{x"([0-9a-fA-F]+)"', text)
    if not candidates:
        raise ValueError('No public MSD11 key candidates in catalogue')
    with zipfile.ZipFile(firmware) as archive:
        members = [n for n in archive.namelist() if n.endswith('/image/upgrade.msd')]
        if len(members) != 1:
            raise ValueError('Expected one upgrade.msd')
        member = members[0]
        with archive.open(member) as stream:
            prefix = stream.read(65536)
        if prefix[:6] != b'MSDU11':
            raise ValueError('Not MSDU11')
        sections = struct.unpack_from('<I', prefix, 18)[0]
        if not 1 <= sections <= 64:
            raise ValueError('Invalid section count')
        pos = 22 + sections * 20
        header_count = struct.unpack_from('<I', prefix, pos)[0]
        if not 1 <= header_count <= 64:
            raise ValueError('Invalid header count')
        pos += 4
        headers = []
        for _ in range(header_count):
            offset, size, name_length = struct.unpack_from('<QIB', prefix, pos)
            pos += 13
            name = prefix[pos:pos + name_length].decode('ascii')
            pos += name_length
            if offset + size > len(prefix) or size < 24:
                raise ValueError('Header exceeds bounded prefix')
            toc = prefix[offset + 8:offset + size]
            if toc[:8] != b'Salted__' or (len(toc) - 16) % 16:
                raise ValueError('Invalid encrypted TOC')
            salt, ciphertext = toc[8:16], toc[16:]
            matches = []
            for family, key in candidates:
                key_bytes = bytes.fromhex(key)
                if len(key_bytes) not in (16, 32):
                    raise ValueError('Unsupported public candidate length')
                iv = hashlib.md5(salt).digest() if len(key_bytes) == 16 else hashlib.sha256(salt).digest()[:16]
                result = subprocess.run(['openssl', 'enc', '-aes-%d-cbc' % (len(key_bytes) * 8),
                    '-d', '-nopad', '-K', key, '-iv', iv.hex()], input=ciphertext,
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True).stdout
                marker = b'Tizen Software Upgrade Tree Binary Format ver. '
                if any(result[at:at + len(marker)] == marker for at in (256, 262, 518)):
                    padding = result[-1]
                    matches.append({'family': family, 'known_toc_marker': True,
                        'valid_padding': 1 <= padding <= 16 and result[-padding:] == bytes([padding]) * padding})
            headers.append({'model': name, 'offset': offset, 'bytes': size, 'matches': matches})
        return {'schema': 1, 'archive': firmware.name, 'member': member, 'sections': sections,
            'header_count': header_count, 'public_candidates_tested_per_header': len(candidates),
            'catalogue_sha256': hashlib.sha256(catalogue.read_bytes()).hexdigest(),
            'headers': headers, 'partitions_extracted': False,
            'interpretation': 'No match only excludes this public candidate set; it is not proof that no key exists.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('firmware', type=Path)
    parser.add_argument('catalogue', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    report = audit(args.firmware, args.catalogue)
    args.out.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))
