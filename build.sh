#!/usr/bin/env bash
# Build the TizenHalo package, run the host tests and verify the result.
# Output: dist/TizenHalo-<version>.tpk (signed with the Tizen SDK default certificate).
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$PWD/.nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
flags=(-c Release --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false)

version=$(sed -n 's/.*<manifest[^>]* version="\([^"]*\)".*/\1/p' app/tizen-manifest.xml)

dotnet build app/TizenHalo.csproj "${flags[@]}"
mkdir -p dist
cp "app/bin/Release/tizen55/io.github.tenda96.TizenHalo-$version.tpk" "dist/TizenHalo-$version.tpk"

dotnet build tests/LayoutTests.csproj "${flags[@]}"
dotnet tests/bin/Release/net8.0/LayoutTests.dll
dotnet tests/bin/Release/net8.0/LayoutTests.dll interpolation
if command -v node >/dev/null 2>&1; then node tests/layout-editor.test.cjs; else echo "node not found: editor test skipped"; fi

python3 tools/verify-package.py "dist/TizenHalo-$version.tpk"
