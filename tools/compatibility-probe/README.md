# Compatibility probe — experimental Tizen 5.0 diagnostic

This is a separate app for collecting the information needed to port TizenHalo.
It is **not an Ambilight build** and does not claim that capture works on the TV.
It targets `tizen50` and manifest API 5.0. It has compiled successfully on the host;
installation and startup on the RU7100 have not yet been verified.

The probe reads a fixed list of capture-related library files as ELF data. It reports
the platform version, process architecture, SHA-256 hashes and relevant **defined**
dynamic symbols. It never loads those libraries or invokes a capture function.
It does not change TV settings or access WLED.

## For the TV owner

1. Sideload `TizenHalo-Compatibility-Probe-0.1.0-tizen50.tpk` with Apps2Samsung.
   It uses the SDK default certificate and needs the same per-TV signing workflow
   as other sideloaded TPKs.
2. Open **TizenHalo Compatibility Probe** and keep its screen open.
3. On a phone/computer on the same network, open the address shown by the TV,
   `http://<TV-IP>:8794/`. If the app cannot detect its address, use the TV's IP
   from its network settings.
4. Download the **compatibility report** and share that JSON with the maintainer.
   Include the exact model and firmware version in your message; those fields
   are not reliably exposed by the platform information API.
5. If requested, download the capture libraries from the same page and send them
   privately for analysis. Missing libraries are a useful finding. Do not upload
   Samsung library binaries to a public GitHub issue or repository.
6. Press Back on the TV to close the diagnostic and stop its server.

The server has no password and runs only while this app is open. It serves the
report and a fixed library allowlist; it has no arbitrary-file endpoint or
settings/write endpoint. Request headers and individual library sizes are bounded.
No report is sent to an external server. The report does not collect serial numbers,
DUID, account details or signing certificates.

## Build and verify

From the repository root, with .NET 8 installed:

```sh
bash tools/compatibility-probe/build.sh
```

This verifies the diagnostic and creates a small owner kit under `dist/`.
For the individual steps:

```sh
export DOTNET_CLI_HOME="$PWD/.dotnet-home"
export NUGET_PACKAGES="$PWD/.nuget"
dotnet build tools/compatibility-probe/TizenHaloProbe.csproj -c Release -p:NuGetAudit=false
dotnet build tools/compatibility-probe/host-tests/HostTests.csproj -c Release -p:NuGetAudit=false
dotnet tools/compatibility-probe/host-tests/bin/Release/net8.0/HostTests.dll
python3 tools/compatibility-probe/verify-package.py
```

The host test optionally accepts a local ELF library path. That verifies the
symbol reader against a real firmware library without executing ARM code.
Tests cover ELF32/ELF64, imported vs defined symbols and malformed bounds.

The package is created under `tools/compatibility-probe/bin/Release/tizen50/`.
It can coexist with TizenHalo: it uses a different package id and port.
