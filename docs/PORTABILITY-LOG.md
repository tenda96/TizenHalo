# Portability work log

## 2026-10-26 — evidence baseline and RGB discovery

- Checked upstream default branches and relevant issues/PR discussions; pinned
  12 implementation references. Curated 14 device entries including Q74T/RU7100,
  S84F, Q80A, QN90A, Q7F, S85F, R95H, The Frame, QN90C, QN90D, Q60D, S95F and
  the separately identified QN75Q7F WLED setup. This is a bounded first pass,
  not an exhaustive list of all Samsung models or every fork.
- Found useful positive full-frame evidence for Q80A/Tizen 6 in an unmerged PR.
  Checked its source: caller-owned image buffers, not native-owned; padded info
  block after an earlier 36-byte overwrite. Native readback contract still incomplete.
- Recorded RGB-positive reports across current Tizen 9/10 devices. Distinguished
  measurement refresh from interpolated output Hz. Recorded QN90A Partner-signing
  failure, S85F unknown failure stage and S95F UI-only/external-source limitations.
- Created `compatibility/catalogue.json` and generated `docs/DEVICES.md`; added
  validation plus build-time stale-table checks. No raw comments/contact data saved.
- Added independent complete RGB-family resolution and diagnostics. Removed the
  forced-RGB startup dependency on full-frame libraries and the unconditional
  video method initialization in the main loop. Preserved Q74T defaults and native
  frame timing; no S84F/Q80A ABI guessed or enabled.
- Wrote staged plan, native contract notes, configuration instructions and a
  handoff for another developer/Claude. RU7100 key/owner work remains separate.

Verification completed:

- Catalogue validation and generated-table freshness: PASS (14 entries, 12 pins).
- Four catalogue guard tests: PASS.
- Main Tizen 5.5 package and .NET host tests compile with zero warnings/errors.
- Host regression suite: PASS, including new RGB resolution/startup tests and
  existing launch routing, frame trace, retry, mapping, crop, settings and QR tests.
- UDP interpolation test: PASS after allowing its loopback socket. The initial
  sandbox run failed at socket bind with error 13; no application changes were
  needed to pass it.
- Shipped browser layout editor test: PASS.
- TPK verifier: PASS, 23 signed-file digests checked. Main package SHA-256:
  `6289f748d437e216f1654209408bb920b86dc92f4fcd3c087aba877e34c736d1`.
- `git diff --check`: PASS.

No new on-device validation, installation, remote setting changes or upstream
publication. The generated package uses the unchanged 1.0.0 manifest version;
it is an experimental local checkpoint, not a claim of a new supported-TV release.
Next work is the opt-in owner RGB test and an independently named S84F full-frame
layout contract; Q80A still needs stronger readback/stride/firmware evidence.

## 2026-10-26 — explicit adapter/runtime profiles and S84F integration

User clarified the packaging objective: TVs with the same usable Tizen runtime
should share a package where possible. Separate **native contracts** do not imply
one permanent application per model. The two 5.5 test profiles below contain
byte-identical application DLLs; the RGB artifact forces the safe first-test path.

Implemented:

- Named `VideoPlaneAbi` contracts for Q74T and S84F, used by the capture wrapper.
  S84F uses shifted flags/pitches, 960×540 caller-owned plane capacities and main
  only. Its uncertain timestamp/flip tail is unread. Both contracts validate
  geometry/strides and copy only returned rows from the caller's own buffers;
  replacement pointers are rejected. No native format sweep was enabled.
- Package metadata profiles loaded once, separately from editable user settings.
  Missing/malformed metadata disables capture and publishes an error. RGB cannot
  be changed to Q74T video/file by user config or legacy control; S84F cannot select
  Q74T-only methods. Method rejection can recover to an allowed method.
- Profile default settings and migration preserve saved WLED/layout while replacing
  incompatible methods and oversized S84F requests. Browser status/method notes
  identify the installed profile and avoid extending Q74T's 15-fps limit to all TVs.
- Reproducible profile build tool, matching generated manifests, profile-aware
  verification and isolated SDK staging. Never overwrite a distributable until
  verification passes. Default reference build/filename remain available.
- Found a real SDK incremental-staging defect: Q74T → RGB → Q74T could retain RGB
  metadata under a Q74T-named TPK. Verification rejected it; per-profile tpkroot
  directories fixed the repeated-switch test. Kept that finding in build docs.

Verification:

- `bash build.sh all`: PASS, including six Python catalogue/manifest tests, all
  three Tizen package builds with zero warnings/errors, shared host regressions,
  synthetic native header/bounds tests, three installed-profile/migration tests,
  missing/malformed metadata cases, loopback interpolation and browser layout test.
- All three TPKs: PASS, 25 signed file digests each, matching runtime/API/profile.
- Rebuilt Q74T after the full profile cycle: PASS (no stale RGB metadata).
- Compared Q74T/RGB application DLL bytes: identical.
- Passed S84F artifact as Q74T to verifier: correctly rejected.
- Catalogue updated: S84F now **experimental full-frame adapter**, not validated.
- No TV installation, live capture, settings changes or upstream publication.
  Q74T refactor needs a device regression; S84F/RGB profiles need owner tests.

Current local artifacts (signature timestamps may change hashes on a later rebuild):

| Profile | Bytes | SHA-256 |
|---|---:|---|
| `q74t-55` | 222322 | `46f8cc8e6b7f57ec0fba4a76368490421cf76f25b6fc6a3a07fe35ea131e16f3` |
| `rgb-55` | 222323 | `9b23fdb335f2e8cf021fadcef5cd8d3951aea85e7d9b4c1a0c4499894ee39a29` |
| `s84f-90` | 222604 | `e65133212e5a541799d2b51018b76bb3f9bf303609ac4f35931ea5c4a91133cd` |

Next: short Q74T regression and opted-in S84F/RGB owner reports. Continue Q80A
static contract review only with additional evidence; its full-frame profile is
still absent. RU7100/MuseL stays blocked on new key or owner-library evidence.
Read `PORTABILITY-HANDOFF.md` and `BUILD-PROFILES.md` before changing package policy.

## 2026-10-26 — public alpha prepared locally

- User asked whether unvalidated profiles could be offered as public test builds.
  Checked GitHub: only stable `v1.0.0` is published, with its reference TPK. No
  upstream release/tag/push was created in this step.
- Prepared `v1.1.0-alpha.1`, numeric Tizen version 1.1.0 and distinct runtime/log
  identity `tizenhalo-1.1.0-alpha.1`, so reports cannot be confused with stable 1.0.0.
- Added tester/package-selection instructions, stable rollback link, short SDR
  protocol, exact report fields and startup/version checks after a full restart.
  Marked all profiles unvalidated for this alpha revision, including Q74T regression.
- Added release notes and a compatibility issue form; it parses as YAML with eight
  distinct data fields. A plain-text issue fallback is also in the instructions.
- Added local-only allowlist packager: requires clean committed source, validates
  profile-aware package digests, factory settings and compiled alpha identity,
  verifies the existing diagnostic, records source commit and SHA256SUMS, then
  produces a small test-kit ZIP. No publication or credentials are needed.
- Verification: `bash build.sh all` PASS with eight Python tests, all three app
  builds with zero warnings/errors, package checks, header/profile/migration
  regressions, loopback interpolation and offline layout-editor test.
- Next publication step, if requested: push the reviewed source and exact tag,
  create a GitHub **pre-release** with these assets/notes, retain stable v1.0.0,
  then collect owner reports before any support promotion.

## 2026-10-26 — alpha published on owner's instruction

- Pushed the source branch and annotated `v1.1.0-alpha.1` tag atomically using Git.
  The release source is exactly `42f4e8bfaec83a696606d85ecf2b8913a20deb33`.
- Created a draft source PR (#1) without merging/replacing main.
- Uploaded ten allowlisted assets to a draft release. GitHub-assigned SHA-256
  digests, sizes, source target and notes all matched the local prepared kit.
- Published the release as a pre-release, explicitly not Latest. Confirmed the
  public release endpoint and all assets. Stable v1.0.0 remains Latest with its
  original artifact digest. No public asset or tag was overwritten.
- Saved `releases/PUBLICATION-v1.1.0-alpha.1.md` with the release, source commit,
  asset inventory, kit checksum, stable baseline and next owner-test tasks.
- No on-device tests or support-status promotions were performed in publication.
