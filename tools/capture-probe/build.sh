#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$PWD/.nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
flags=(-c Release --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false)
dotnet build tools/capture-probe/host-tests/HostTests.csproj "${flags[@]}"
dotnet tools/capture-probe/host-tests/bin/Release/net8.0/HostTests.dll
dotnet build tools/capture-probe/TizenHaloCaptureProbe.csproj "${flags[@]}"
python3 tools/capture-probe/verify-package.py
mkdir -p dist
cp tools/capture-probe/bin/Release/tizen50/io.github.tenda96.TizenHalo.CaptureProbe-0.1.0.tpk \
   dist/TizenHalo-Capture-Probe-0.1.0-tizen50.tpk
echo "built dist/TizenHalo-Capture-Probe-0.1.0-tizen50.tpk"
