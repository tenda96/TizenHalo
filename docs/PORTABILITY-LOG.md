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
