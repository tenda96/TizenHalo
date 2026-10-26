#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$PWD/.nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
flags=(-c Release --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false)
dotnet build tools/compatibility-probe/TizenHaloProbe.csproj "${flags[@]}"
dotnet build tools/compatibility-probe/host-tests/HostTests.csproj "${flags[@]}"
dotnet tools/compatibility-probe/host-tests/bin/Release/net8.0/HostTests.dll
python3 tools/compatibility-probe/verify-package.py
mkdir -p dist
cp tools/compatibility-probe/bin/Release/tizen50/io.github.tenda96.TizenHalo.Probe-0.1.0.tpk dist/TizenHalo-Compatibility-Probe-0.1.0-tizen50.tpk
python3 - <<'PY'
from pathlib import Path
import hashlib
import json
import zipfile
root = Path('tools/compatibility-probe')
package = Path('dist/TizenHalo-Compatibility-Probe-0.1.0-tizen50.tpk')
out = Path('dist/TizenHalo-RU7100-diagnostic-kit.zip')
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    z.write(package, package.name)
    z.write(root / 'README.md', 'README.md')
    z.write(root / 'OWNER-MESSAGE.txt', 'OWNER-MESSAGE.txt')
    z.write('LICENSE', 'LICENSE')
with zipfile.ZipFile(out) as z:
    assert z.testzip() is None
print(json.dumps({'kit': str(out), 'bytes': out.stat().st_size,
    'sha256': hashlib.sha256(out.read_bytes()).hexdigest()}, indent=2))
PY
