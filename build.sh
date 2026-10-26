#!/usr/bin/env bash
# Build the TizenHalo package, run the host tests and verify the result.
# Output: dist/TizenHalo-<version>.tpk (signed with the Tizen SDK default certificate).
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$PWD/.nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
flags=(-c Release --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false)

python3 tools/compatibility-catalogue.py --check
python3 -m unittest discover -s tests -p 'test_*.py'

python3 tools/build-profile.py "${1:-q74t-55}"

dotnet build tests/LayoutTests.csproj "${flags[@]}"
dotnet tests/bin/Release/net8.0/LayoutTests.dll
for profile in q74t-55 rgb-55 s84f-90; do
  dotnet tests/bin/Release/net8.0/LayoutTests.dll profile "compatibility/profiles/$profile.json"
done
dotnet tests/bin/Release/net8.0/LayoutTests.dll profile-invalid dist/.missing-capture-profile.json
dotnet tests/bin/Release/net8.0/LayoutTests.dll profile-invalid app/config.example.json
dotnet tests/bin/Release/net8.0/LayoutTests.dll interpolation
if command -v node >/dev/null 2>&1; then node tests/layout-editor.test.cjs; else echo "node not found: editor test skipped"; fi
