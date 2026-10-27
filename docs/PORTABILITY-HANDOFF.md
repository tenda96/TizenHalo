# Portability handoff

Checkpoint: 2026-10-27 (RU7100 capture CONFIRMED working; real ru7100-50 app profile built). Branch `research/ru7100-compatibility`.
Original online baseline: `8e4f9107bf05c444dedff5852cd806b5d633bab3`.
RU7100 diagnostic commit: `da7ad2c`. Inspect `git log -3` for the current checkpoint
commit; do not reset the branch to the online baseline.

**Latest: experimental RU7100 CAPTURE probe prepared locally, offline only.**
`tools/capture-probe/` loads the legacy library directly (no modern-library dependency) and makes
three serialized `secvideo_api_capture_video_main` calls at 640×360 behind an explicit owner action,
with no LED output. See `docs/RU7100-OWNER-REPORT.md` (section "Capture probe prepared") and
`tools/capture-probe/README.md`. v0.1 was run on the TV: capture SUCCEEDED (rc=0, legacy library loaded directly, not protected,
caller pointers kept, header contract confirmed on hardware), but the frame was dark because the owner
sat in the app with no video playing, so the video plane was black and colour order is still unresolved.
v0.3 (built, host-tested, NOT yet run on the TV) has the probe PAINT full-screen red/green/blue on its
own window and capture each, plus one video-plane capture, so the owner needs no photo and no
background (Tizen pauses backgrounded UI apps). The known painted colour settles the VU/UV order and
whether screen capture includes graphics. Not published, no new tag/PR. The three capture layers stay distinct: static analysis of the owner binary → offline
host tests → the still-pending real capture on the TV. Next real step is an owner capture run; only a
successful one justifies an LED-output profile. Do not route enum-0 output through the NV12 extractor
until the owner's capture settles the VU/UV order.

**Newest input: RU7100 owner files received.** Read `docs/RU7100-OWNER-REPORT.md`
before continuing. The read-only probe runs on reported Tizen 5.0 / 32-bit; three
unique ARM32 libraries match the report hashes. Model/firmware attribution is
from the conversation, not independently populated report fields. Legacy capture
wrapper/core and caller-owned header were inspected. The modern capture/RGB
names were not readable in the probed paths. No capture was invoked, no 5.0
main-app profile has been built, and no public alpha artifact was replaced.

The key new pitfall is color order: this library explicitly handles **NV21** yet
reports the same enum 0 as planar 420. Do not route it into the existing NV12 zone
extractor without proving/handling VU/UV order. Header +24/+28/+2c are not modern
pitch/interlace outputs in this copy path. The current loader also requires the
modern library before choosing a legacy symbol; that must be separated for RU7100.
Next: explicit direct-legacy Tizen 5.0 profile/probe, synthetic contract/color tests,
then a few serialized owner capture calls. Firmware decryption is no longer the
blocker for this API work, although the encrypted firmware itself remains unresolved.

Private inputs, full decompiler evidence and REA snapshots are outside Git in
`../tizen-ru7100-research/owner-report-2026-10-27/`. Both opened REA targets were
closed with snapshots saved. The capture core is `0x13798`, main wrapper `0x158f8`,
crop body `0x157c8`; avoid the ambiguous crop PLT symbol name. No proprietary
binaries/decompiled functions should be committed or attached to public issues.

**Publication completed:** the owner authorized upload, and
[v1.1.0-alpha.1](https://github.com/tenda96/TizenHalo/releases/tag/v1.1.0-alpha.1)
is public with ten verified assets. Source tag remains pinned to
`42f4e8bfaec83a696606d85ecf2b8913a20deb33`; later branch commits document the
publication only. Stable v1.0.0 remains Latest and main is unchanged. Draft
[PR #1](https://github.com/tenda96/TizenHalo/pull/1) contains the source changes.
Read [the publication record](releases/PUBLICATION-v1.1.0-alpha.1.md) before
rebuilding or considering a second upload. Do not recreate or move the alpha tag.

**Latest stage:** experimental S84F full-frame adapter and profile-aware builds
are now implemented and tested offline. Start with `docs/BUILD-PROFILES.md` for
the current package boundary. The user clarified that TVs with the same usable
runtime should share a package where possible: do not create a permanent package
for every TV model. Q74T-5.5 and RGB-5.5 currently have identical application DLLs;
their separate metadata/test artifacts are for safe first-owner trials.

**Public-alpha preparation:** source now targets numeric package 1.1.0 and runtime
`tizenhalo-1.1.0-alpha.1`. The eight Python tests plus full build/host suite pass.
`docs/TESTING.md`, `docs/releases/v1.1.0-alpha.1.md` and a compatibility issue form
are prepared. After a clean source commit, `python3 tools/prepare-prerelease.py`
verifies the three app TPKs plus the separate probe, checks factory config/runtime
identity, and creates an allowlisted kit under `dist/v1.1.0-alpha.1/` with source
commit, checksums and ZIP. It refuses a dirty source tree or an existing output.
The preparation tool itself performs no push/tag/release publication. At that
preparation checkpoint only stable v1.0.0 existed; the owner subsequently authorized
publication and the current public state is recorded above.

## User objective and present boundary

Long term: cover as many Samsung/Tizen TVs as evidence permits, sharing settings,
layout, color processing and LED output. Current request: inventory published
working methods, start a staged integration and preserve a reproducible handoff.
No universal Tizen-version support is claimed. No TV installation or new live
capture test was performed in this stage. The user handles Apps2Samsung sideload.

RU7100 owner supplied a successful read-only diagnostic and libraries on 2026-10-27,
but has not supplied a capture test. Reported firmware attribution is
T-MSLAKUC-1500.5 / Tizen 5.0. Expanded public-key audit (21 candidates
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

## First integration (commit ada9578)

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
- At that checkpoint no full-frame S84F/Q80A adapter was enabled. No GPL implementation copied.

## Current integration after ada9578

- `VideoPlaneAbi.cs`: independently named Q74T/S84F ARM32 header contracts,
  caller-owned capacities, pointer/stride/geometry checks. Q74T retains its offsets;
  S84F shifts the format/flags/pitches and has no trusted timestamp/flip offset.
- `VideoPlaneCapture.cs`: uses the installed layout, copies only validated returned
  rows from its own buffers, rejects replacement pointers and restricts entry points.
  S84F is main-only, 960×540 maximum; automatic color-format sweeps stay disabled.
- `CaptureProfile.cs`: loads a validated package profile once from
  `res/capture-profile.json`. Malformed/missing metadata disables capture, never
  guesses Q74T. Browser/config/control cannot select a foreign writable layout.
- `LocalSettings.cs`: profile defaults, valid method filtering and saved-settings
  migration preserve WLED/custom layout while replacing incompatible capture values.
- `compatibility/profiles/*.json`, `tools/build-profile.py`, project properties:
  three explicit test profiles with generated matching manifests, isolated SDK
  staging and profile-aware TPK verification before distribution-copy.
- `Settings.html`: shows the installed profile, lists only permitted methods and
  no longer describes 15 fps as a universal Samsung limit.
- New host tests cover binary header offsets/bounds, unknown tail, package startup
  constraints, method rejection/recovery, saved layout preservation and invalid
  metadata. Python checks cover matching manifest API/identity and unchanged sources.
- All three TPKs compile with zero warnings/errors and pass 25 signed-digest checks.
  Shared regressions, UDP interpolation and browser editor checks pass. A repeated
  profile switch caught/fixed stale SDK resource staging; the verifier also rejects
  a S84F artifact passed as Q74T. No on-device test of this revision yet.

For an explicit RGB test, build the locked `rgb-55` package, or use the install-time `captureBackend: ppi` setting in
an ignored `app/config.json`; preserve the owner's actual WLED/layout values.
The persistent browser backend selector is not implemented yet. Do not distribute
the default Q74T full-frame build to an unknown TV as a universal package.

## Offline verification commands

From the repository root:

```sh
python3 tools/compatibility-catalogue.py --check
python3 -m unittest discover -s tests -p 'test_compatibility_catalogue.py'
bash build.sh all
```

`build.sh all` validates the table, runs catalogue/profile guards, compiles all three targets,
runs host tests, the UDP interpolation check, the browser layout test and the
package verifier. It builds profile-suffixed TPKs and a Q74T alias `dist/TizenHalo-1.1.0.tpk`. The current checkpoint is
an experimental portability build, not a new multi-TV support release.

In this workspace the existing NuGet cache is at
`../tizen-q74t-research/ambilight-port/.nuget`; use `NUGET_PACKAGES` pointing there
if needed. Sandboxed UDP loopback binding can return SocketException 13; rerun
the interpolation check with local-socket permission rather than changing the
sampler. This is a test-environment error, not evidence of a device capture failure.

## Next concrete actions

1. Continue RU7100 from its newly received native files and the detailed review.
   Also obtain an owner diagnostic for a known RGB-positive TV and match actual exports,
   process architecture and native permissions. Build an explicitly forced RGB
   test profile; record condition, fresh source colors and complete-set refresh.
2. Test the built S84F-9 profile with an owner: exact firmware, launch/service,
   library permissions, fresh SDR picture/colors, returned dimensions/strides and
   sustained rate. Synthetic layout tests already pass; timestamp/flip tail remains
   unread until proven. Run a Q74T device regression for the refactored adapter too.
3. Continue Q80A contract research from the pinned PR. Establish geometry, pitches,
   pointer lifetime and exact firmware before implementing native readback.
4. Package/runtime profiles now exist. Design a shared-runtime first-launch
   selection flow and persistent backend choice to consolidate test variants where
   possible, using actual device identification evidence. Unknown firmware must not silently pick an ABI
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
