# Portability handoff

Checkpoint: 2026-10-26. Branch `research/ru7100-compatibility`.
Original online baseline: `8e4f9107bf05c444dedff5852cd806b5d633bab3`.
RU7100 diagnostic commit: `da7ad2c`. Inspect `git log -3` for the current checkpoint
commit; do not reset the branch to the online baseline.

## User objective and present boundary

Long term: cover as many Samsung/Tizen TVs as evidence permits, sharing settings,
layout, color processing and LED output. Current request: inventory published
working methods, start a staged integration and preserve a reproducible handoff.
No universal Tizen-version support is claimed. No TV installation or new live
capture test was performed in this stage. The user handles Apps2Samsung sideload.

RU7100 owner has been contacted and has not yet tried the app. Its installed
firmware is T-MSLAKUC-1500.5 / Tizen 5.0. Expanded public-key audit (21 candidates
from 24 repositories/forks) failed to decrypt the header; Q74T positive control
succeeded. Do not repeat the completed audit without genuinely new candidates.
The read-only diagnostic package and owner instructions are already in
`tools/compatibility-probe/`. Never test the unknown RU7100 ABI with Q74T offsets.

## Read in this order

1. `docs/PORTABILITY-PLAN.md`: stages and completion gates.
2. `docs/DEVICES.md`: 14 device records, rates, failures and source links.
3. `compatibility/catalogue.json`: authoritative editable evidence; 12 commit pins.
4. `docs/CAPTURE-CONTRACTS.md`: interfaces, known offsets and missing ABI evidence.
5. `docs/PORTABILITY-LOG.md`: what changed and what was actually verified.

Only the Q74T firmware is validated in TizenHalo. Upstream author/user results
must not be promoted to local support because the host compiler or resolver passed.
PR #14/#15 in reisxd and PR #2 in lowryn were **open**, not merged, at review time.
Follow-up comments matter: the QN90A owner already had a Partner certificate;
The Frame author's initial on-boot claim was retracted after testing.

## Code already changed in this stage

- `RgbMeasureApi.cs`: resolve complete condition/position/pixel triplets, priority
  `ppi_ve` → `ve` → `cs_ve`. No native invocation in the resolver, no mixed families.
- `PpiCapture.cs`: use that resolver with the existing worker, buffers, condition
  validation, slot cap and park lifecycle; expose selected family/missing members.
- `CaptureBackendStartup.cs` / `Program.cs`: forced `ppi` no longer loads or gates
  on video-plane/file libraries. RGB initialization stays behind existing runtime
  gates. Video method selection runs only in video mode; file init is lazy on a
  runtime switch. Default remains Q74T `video_plane`.
- `Telemetry.cs`: expose RGB family/symbol/error diagnostics in `/api/status` and
  avoid reporting the Q74T ABI when RGB/file is active.
- Host tests cover partial/missing families, priority, no family mixing and RGB
  startup with both unrelated libraries unavailable.
- Catalogue generator and validation prevent a moving branch as a commit pin,
  missing references, duplicate devices and upstream-only promotion to local support.
- No full-frame S84F/Q80A adapter has been enabled. No GPL implementation copied.

For an explicit RGB test, use the install-time `captureBackend: ppi` setting in
an ignored `app/config.json`; preserve the owner's actual WLED/layout values.
The persistent browser backend selector is not implemented yet. Do not distribute
the default Q74T full-frame build to an unknown TV as a universal package.

## Offline verification commands

From the repository root:

```sh
python3 tools/compatibility-catalogue.py --check
python3 -m unittest discover -s tests -p 'test_compatibility_catalogue.py'
bash build.sh
```

`build.sh` validates the table, runs catalogue guards, compiles the Tizen app,
runs host tests, the UDP interpolation check, the browser layout test and the
package verifier. It builds `dist/TizenHalo-1.0.0.tpk`. The current checkpoint is
an experimental portability build, not a new multi-TV support release.

In this workspace the existing NuGet cache is at
`../tizen-q74t-research/ambilight-port/.nuget`; use `NUGET_PACKAGES` pointing there
if needed. Sandboxed UDP loopback binding can return SocketException 13; rerun
the interpolation check with local-socket permission rather than changing the
sampler. This is a test-environment error, not evidence of a device capture failure.

## Next concrete actions

1. Obtain an owner diagnostic for a known RGB-positive TV and match actual exports,
   process architecture and native permissions. Build an explicitly forced RGB
   test profile; record condition, fresh source colors and complete-set refresh.
2. For full frames, start with an independently named S84F MIT layout adapter:
   factor header read/write logic, verify all offset/alignment and capacities with
   synthetic tests, preserve the Q74T layout and require explicit selection.
3. Continue Q80A contract research from the pinned PR. Establish geometry, pitches,
   pointer lifetime and exact firmware before implementing native readback.
4. Add explicit persistent backend/profile selection and package/runtime profiles
   after those contracts exist. Unknown firmware must not silently pick an ABI
   from Tizen version or model year. Run launcher/service checks per profile.
5. Owner tests: identified SDR source first, bounded capture, correct colors and
   mapping, stable freshness, output on/off, stop, standby and icon restart. Then
   app/protected/HDR sources separately. Record package commit and firmware.

## Maintenance and repository hygiene

Edit JSON, regenerate the device table, run `--check`, update this handoff/log.
Do not schedule monitoring or message owners automatically. Keep proprietary TV
libraries, full firmware, signing keys, private IDs/IPs and raw comment HTML out
of the public repository. GPL sources remain linked references; use independently
written adapters or explicitly resolve licensing before incorporating code.

Local firmware audit files remain outside this repo under
`../tizen-ru7100-research/`, with its own `PUNTO-DI-RIPRESA.txt`. This branch should
not require those binary artifacts to compile or run catalogue checks.
