# Published: v1.1.0-alpha.1

Published 2026-10-26, 22:51:55 UTC (23:51:55 Europe/Rome).

- Release: https://github.com/tenda96/TizenHalo/releases/tag/v1.1.0-alpha.1
- State: public **pre-release**, not Latest.
- Annotated Git tag: `v1.1.0-alpha.1`.
- Tagged source commit: `42f4e8bfaec83a696606d85ecf2b8913a20deb33`.
- Source branch: `research/ru7100-compatibility`, pushed with Git.
- Draft review PR: https://github.com/tenda96/TizenHalo/pull/1
- Stable Latest remains `v1.0.0`; main was not merged or rewritten.

The owner authorized the upload. Source branch and annotated tag were pushed
atomically with Git. The GitHub CLI/API created a draft, uploaded the explicit
allowlist, and verified all ten uploaded SHA-256 digests and file sizes against
the local kit before publishing with `prerelease=true` and `make_latest=false`.
The public release endpoint was then checked independently.

## Public assets

1. `TizenHalo-1.1.0-q74t-55.tpk`
2. `TizenHalo-1.1.0-rgb-55.tpk`
3. `TizenHalo-1.1.0-s84f-90.tpk`
4. `TizenHalo-Compatibility-Probe-0.1.0-tizen50.tpk`
5. `TizenHalo-v1.1.0-alpha.1-test-kit.zip`
6. `TESTING.md`
7. `RELEASE-NOTES.md`
8. `SOURCE.json`
9. `SHA256SUMS`
10. `LICENSE`

The kit is 689,519 bytes. ZIP SHA-256:
`6350cecc7e3ce471916c3779aa916d9ee56e7dcc4c9b5debe9d0aa87ef7794b7`.

The original stable asset remains 218,294 bytes, SHA-256:
`576bce4434584147e5274363dd1f0a8e7054a5a347c80f95a7bc4de55605b04b`.

No TV was installed, captured or changed during publication. No new on-device
validation is claimed. The release tag must stay pinned to the recorded source;
later documentation/checkpoint commits on the branch do not change the binary
provenance. Do not rebuild/replace these public assets silently under the same tag.

Next: collect opt-in owner reports using `TESTING.md`, run the Q74T regression,
and evaluate S84F/RGB results before promoting any support status. The compatibility
issue form is included in the draft source PR; the published testing guide also
provides a plain-text report fallback for the current default branch.
