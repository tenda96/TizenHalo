# Portability plan

Started: 2026-10-26. Working branch: `research/ru7100-compatibility`.
Baseline: `8e4f9107bf05c444dedff5852cd806b5d633bab3`; the separate RU7100
diagnostic was added in `da7ad2c`.

## Objective

Extend TizenHalo to additional Samsung TVs using capture paths already described
in public implementations and device reports. Keep the WLED/layout/color pipeline
shared, and identify native capture adapters explicitly. Universal Tizen coverage
is a long-term objective, not a claim that a version-wide adapter is already valid.

## Step 1 — Evidence and device catalogue (first pass complete)

- Inspect upstream source, manifests, device reports and follow-up comments.
- Record repository commit, file/issue URL, exact model, firmware when supplied,
  current platform version, capture method and signing/runtime requirements.
- Keep positive and negative reports, including conflicting reports.
- Distinguish local TizenHalo tests, upstream author tests, user reports,
  implementation-only evidence and unresolved research.
- Record capture FPS separately from sampled-point refresh and LED output Hz.

Done when: a source-linked catalogue and readable matrix exist, claims can be
traced to evidence, and untested entries cannot be presented as supported devices.

## Step 2 — RGB API families (offline integration complete; owner tests pending)

- Reuse TizenHalo's existing sampler, buffers, slot limits and worker lifecycle.
- Resolve complete `ppi_ve`, `ve` or `cs_ve` condition/position/pixel families;
  never combine functions from different families.
- Preserve the existing `ppi_ve` path when available. Report the selected family,
  missing symbols and condition-validation failures.
- Write the resolver independently in this MIT project. GPL projects remain
  technical references; do not paste their implementation into this branch.
- Make forced RGB startup independent of both video-plane and file-capture libraries;
  initialize native measurement only through the sampler's existing lifecycle.
- Verify missing/partial/complete-family behavior on the host and compile the
  package. New TV coverage remains experimental until an owner confirms it.

Done when: host checks and package checks pass, the existing path is preserved,
and diagnostics identify the family without claiming a successful TV test.

## Step 3 — Full-frame adapters (S84F integrated offline; Q80A in research)

- Keep Q74T's caller-owned-buffer ABI as the verified reference adapter.
- Review the upstream S84F/Tizen 9 layout independently of the Q74T layout.
- Review the Q80A/Tizen 6 port, especially caller-owned buffers, structure size,
  lifetime, image format and error handling. A padded structure alone is not an ABI.
- Store the adapter contract: symbols, library alternatives, pointer width,
  input/output offsets, maximum buffers/strides, ownership, required privileges,
  serialization and proven model/firmware scope.
- Select unknown full-frame ABIs only after evidence and explicit experimental
  configuration; never guess a layout from the Tizen major version alone.

Done when: each implemented adapter has documented provenance, synthetic
readback/bounds tests, a compatible package, and an opt-in owner test protocol.

S84F has reached the offline gate with its named ARM32 layout and explicit Tizen 9
test profile. Its uncertain timestamp/flip tail is unread; owner validation is
pending. Q80A has not reached this gate.

## Step 4 — Packaging and runtime compatibility (three test profiles built)

- Check the .NET target, manifest API, launcher/service identity and dependency
  file names for each package profile.
- Preserve the current Q74T build while new profiles are tested.
- Treat install/startup success separately from permission to load or call capture.
- Record signing requirements per firmware; do not assume a partner certificate
  solves every `Operation not permitted` or `rc=-95` report.
- Older Tizen releases may need a separate host instead of the present .NET app.
- Prefer a few runtime packages with multiple validated capture paths. The user
  explicitly clarified that TVs on the same 5.5 runtime should share a package
  where possible. The separate RGB-5.5 artifact is for first-owner testing, not a
  permanent one-package-per-TV architecture; its DLL is identical to Q74T's.

Done when: profile-specific packages pass offline verification and their actual
device startup/capture results are recorded. Host compilation is not enough.

`q74t-55`, `rgb-55` and `s84f-90` compile and pass profile-aware package checks.
Shared host tests cover package settings migration and restricted native methods.
These revisions still need device regression/owner tests; no new model is promoted.

## Step 5 — Owner validation (waiting for devices)

- Start with the read-only compatibility report where the ABI is unknown.
- Ask for a short, identified SDR source and a bounded opt-in capture test.
- Verify colors/orientation, black bars, HDR behavior where applicable, stable
  capture, pause/standby, startup from the icon and stop/output behavior.
- Never mix several apps or sources while attributing a capture result.
- Keep native frame/measurement rates distinct from interpolation output.

Done when: the owner result, package commit, firmware and limitations are linked
from the catalogue. Only then promote an entry to validated TizenHalo support.

## Step 6 — Public documentation and maintenance

- Generate the public compatibility table from the machine-readable catalogue.
- Log source checks, changes, verification and unresolved questions.
- Keep a handoff with current files, tested commands and the next concrete action.
- Re-check evidence when a source or firmware changes; do not automatically
  upgrade support status from a newer commit or an exported symbol.
- Keep firmware binaries, proprietary libraries, certificates and private device
  identifiers out of this repository.

## Current constraints

The RU7100 MuseL firmware header did not decrypt with the original 19 public
candidates or the expanded 21-candidate set collected from 24 repositories/forks.
The owner has been contacted; its diagnostic report is pending. That research
continues separately while other documented capture paths are inventoried here.

No new TV tests or installations are part of the current offline stages.
