# Capture profiles and package targets

Checkpoint: 2026-10-26. These are development/test profiles, not three new
validated device releases. The production reference remains the Q74T firmware.

## One package per TV is not the objective

TVs sharing a usable runtime can share a package and capture implementations.
Installed Tizen version alone does not identify the private native buffer layout,
however. Keep **runtime target**, **native capture contract** and **tested model /
firmware** distinct. The goal is a small number of runtime packages with explicit
capture choices, not a separate permanent application for every Samsung SKU.

The `q74t-55` and `rgb-55` packages contain the **same compiled application DLL**.
Their installed profile metadata and test-menu label differ. The RGB-only variant
is a bounded first-test artifact: it cannot accidentally initialize the Q74T
full-frame ABI on an unknown TV. It can later be consolidated with a common 5.5
package once the initial selection/identification flow has device evidence.
The existing Q74T profile already permits an explicit `captureBackend: ppi`
install-time setting; it must not silently assume a different TV has Q74T buffers.

S84F has a separate Tizen 9 runtime/dependency target as well as a different native
header. Neither its package nor the RGB package is validated on another TV yet.
The RGB package's 5.5 requirement is a minimum manifest/runtime target, not a
promise that every newer Samsung runtime loads and runs it unchanged.

## Current test profiles

| Profile | Project / manifest | Capture contract | Allowed full-frame methods | Status |
|---|---|---|---|---|
| `q74t-55` | `tizen55` / API 5.5 | Q74T caller-owned ARM32; RGB/file may be explicitly selected | Existing seven Q74T methods | Reference firmware; this refactor still needs a device regression run |
| `rgb-55` | `tizen55` / API 5.5 | RGB triplets only; no full-frame ABI | None | Experimental owner-test profile |
| `s84f-90` | `tizen90` / API 9.0 | Separate S84F caller-owned ARM32 header, up to 960×540 | `main` only | Experimental; compiled and checked offline, no TizenHalo device test |

All share package/application/service IDs. Install **one at a time**; do not expect
side-by-side apps. Existing saved WLED, color and layout settings are retained by
the app; incompatible capture methods are normalized to the new profile default,
and an old oversized S84F request resets to 960×540. A clean uninstallation may
remove data depending on the installation tool/TV; application migration cannot
guarantee persistence through a tool that deletes that data.

The RU7100/Tizen 5.0 artifact remains the separate **read-only compatibility
probe**. No full application profile exists for its unrecovered capture ABI.
Q80A/Tizen 6 remains research, not a generated full-frame profile.

## Build

```sh
bash build.sh                  # Q74T reference + host tests
bash build.sh rgb-55           # RGB test package + host tests
bash build.sh s84f-90          # S84F test package + host tests
bash build.sh all              # all three serially + shared host tests
```

For packaging alone: `python3 tools/build-profile.py PROFILE` (or `all`). This
does not run the full host suite. Requires the .NET SDK and the existing Tizen SDK
NuGet dependency; NuGet access is needed if a runtime's dependencies are not cached.
No TV connection or installation is performed by these commands.

The tool reads `compatibility/profiles/*.json`, generates a matching manifest in
ignored `dist/.profiles/`, then supplies the profile and manifest to MSBuild. It
never rewrites `app/tizen-manifest.xml` or the user's ignored `app/config.json`.
The project's default `dotnet build` remains Q74T/5.5. A direct S84F MSBuild call
without its generated matching manifest is rejected.

Artifacts:

```text
dist/TizenHalo-1.0.0-q74t-55.tpk
dist/TizenHalo-1.0.0-rgb-55.tpk
dist/TizenHalo-1.0.0-s84f-90.tpk
```

The Q74T artifact is also copied to the existing `dist/TizenHalo-1.0.0.tpk` name.
Version 1.0.0 is unchanged; filenames distinguish these local development builds.
An artifact is copied into `dist` only **after** its profile-aware verification
passes. The SDK's existing default-certificate signing remains unchanged; device
installation/signing rules are separate, and no certificate material is in Git.

SDK staging directories are profile-specific (`tpkroot-PROFILE`). A real repeated
Q74T → RGB → Q74T build exposed the SDK's incremental cache retaining RGB metadata
in a Q74T-named package; the package verifier caught it. Isolated staging fixes
that cross-profile error. Keep the verifier in the build path.

## Runtime selection boundary

`res/capture-profile.json` is package metadata, separate from browser-editable
settings. It is loaded once before defaults/settings. Missing, malformed or
unknown combinations disable capture and publish an initialization error; they
must never fall back to the Q74T native layout. The same process cannot reload a
different ABI while native buffers are active.

The RGB-only profile forces `ppi` even if user config or the development control
channel requests video/file capture. The S84F profile exposes only `main` for full
frames; Q74T-only method IDs, legacy functions and oversized requests are rejected.
The browser lists profile-specific methods and shows the installed profile in status.

S84F uses a separate header contract: shifted format/protection/interlace/pitches,
caller-owned capacities of 960×540 plus half-size chroma, unchanged-pointer check,
and geometry/stride rejection before copy. Its unproven timestamp/flip tail is
left zeroed and unread. Automatic color-format sweeps remain disabled. Returned
rows alone are copied from the app's own buffers; returned replacement pointers
are never followed. These are offline safety/contract checks, not proof of a
compatible retail firmware or of a sustained capture rate.

## Owner-test gate

1. Obtain the exact SKU, installed OS, firmware and the read-only library report.
2. Choose a matching **test** profile explicitly; do not pick full-frame ABI by year.
3. Keep the known-working package available for rollback; use the owner's normal
   sideload/certificate procedure. No firmware modification is part of this work.
4. Start with an identified SDR source for a short bounded test. Save `/api/status`
   and sampler/full-frame counters, returned geometry, actual fresh colors and errors.
5. Separately check mapping, output off/on, stop/standby, icon restart and sustained
   measurements. Then test app/protected/HDR sources individually.
6. Link the result and package/source commit in the evidence catalogue before
   promoting support. The S84F timestamp offset remains unknown until exact evidence.

See [contracts](CAPTURE-CONTRACTS.md), [device catalogue](DEVICES.md) and
[handoff](PORTABILITY-HANDOFF.md).
