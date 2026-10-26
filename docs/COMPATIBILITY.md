# Compatibility work

Compatibility is tracked by **model, firmware family, native ABI and runtime**.
A Tizen version number or the presence of a function name is not enough to select
a writable native structure safely. A successful host build is not a TV validation.

## Current evidence

| Target | Evidence | TizenHalo status |
|---|---|---|
| QE55Q74T, T-NKMDEUC-2743.0, Tizen 5.5 | Firmware ABI analysis and on-device capture tests | Existing supported reference device |
| Other Tizen 5.5 TVs | No device-specific report yet | Unverified; do not assume the Q74T's firmware ABI |
| UN43RU7100FXZC, T-MSLAKUC-1500.5, Tizen 5.0 | Official firmware downloaded and identified; encrypted header audited | Research only; separate diagnostic package ready, capture ABI not recovered |
| S84F, Tizen 9 | TizenAmbilight upstream reports working video-plane and RGB sampling | Candidate for a separate adapter; not verified in TizenHalo |
| Tizen 6.5+ with RGB sampling exports | HyperTizen/TizenGlow publish sampling implementations | Candidate for a sampling adapter; no blanket TizenHalo support claim |
| Earlier Tizen releases | Runtime and capture interfaces not assessed | Long-term research; may require a different host/package |

Samsung's [model-group table](https://developer.samsung.com/smarttv/develop/specifications/tv-model-groups.html)
lists the 2019 RU7100 family under Tizen **5.0**. The 2020 Q74T is on 5.5.
The current main application's project and manifest target **5.5**, so lowering
the package requirement is one necessary change for a 5.0 port, but it does not
establish that the native capture ABI matches.

## UN43RU7100 firmware investigation — 2026-10-26

The exact firmware is linked by the model's
[official Samsung Canada support page](https://www.samsung.com/ca/support/model/UN43RU7100FXZC/).

- Firmware: **T-MSLAKUC 1500.5**, build date **2024-10-25**.
- Official archive path: `FM/202412/20241209180727804/T-MSLAKUC.zip`.
- Archive size: **1,305,441,418 bytes**.
- Archive SHA-256: `d69cc3386fb2b5d7038cbe2cff4cd01dcfcca9d7525e4f9346ab79f927f9b5c8`.
- Container: **MSDU11**, eight sections, one model header.
- `info.txt` identifies **MuseL Rel key**, unlike the Q74T's NikeM family.
- None of the 19 public MSD11 candidates in the inspected unixtract catalogue
  decrypts a header with the expected TOC marker. The same audit succeeds with
  **NikeM** on the existing Q74T firmware, including valid padding.
- Therefore the RU7100 root filesystem and capture libraries have **not** been
  recovered from this encrypted download. No ABI, resolution or fps claim is
  made for that TV.

Reproduce the bounded header audit with `tools/audit-msd-header.py` and a local
checkout of [unixtract](https://github.com/theubusu/unixtract). The inspected
catalogue comes from commit `3310492cf19c56617acfe84f2f36f14aaab0df5b`.
The audit does not print key material or extract/modify firmware partitions.
Failure only excludes that candidate set; it does not prove no other key exists.

The next practical input is a library report and, if readable, the capture libraries
from the owner's running TV. The [separate compatibility probe](../tools/compatibility-probe/README.md)
targets Tizen 5.0, inventories files without calling native capture, and allows
the owner to download a fixed set of libraries for private analysis.
It does not require root. Whether the retail sandbox permits those reads is a
device result, and permission failures are recorded in the report.

## Public implementations reviewed

| Project | Published path | Scope and reuse |
|---|---|---|
| [TizenAmbilight](https://github.com/RitwickVerma/TizenAmbilight) | Full video plane; `ppi_ve_*` RGB fallback | MIT; already the source of TizenHalo. Its Tizen 9 layout must remain distinct from the verified Q74T layout. |
| [HyperTizen](https://github.com/reisxd/HyperTizen) | `cs_ve_*` / `ve_*` RGB sampling | GPL-3.0; technical reference, no implementation copied into this MIT branch. |
| [TizenGlow](https://github.com/DuckieOnQuacks/TizenGlow) | Complete RGB symbol-family discovery (`ppi_ve`, `ve`, `cs_ve`) and point sampling | GPL-3.0; technical reference, no implementation copied into this MIT branch. README requires Tizen 6.5+. |

The RGB interface measures a limited number of blocks. It is a different backend
from full-frame capture and needs its own rate/quality reporting. A complete
symbol family is a useful discovery result, not proof of a compatible condition
structure or working measurements. The diagnostic inventories all three prefixes.

## Implementation order

1. Collect model, firmware, process/ELF architecture, library hashes and exports.
2. Recover the native ABI from those exact libraries: argument sizes, pointer
   width, buffer capacities, returned format/stride, ownership and lock lifecycle.
3. Add an explicitly identified capture adapter with offline bounds/format tests.
   Unknown ABI selection must not silently use the Q74T structure.
4. Build for the device's supported .NET/runtime and manifest API. Keep the
   known-working Q74T package behavior while the new adapter is experimental.
5. Ask the owner to run a short opt-in capture test, then verify stable capture,
   colors, black bars, standby and startup. Record source and firmware in results.
6. Promote only verified model/firmware combinations; expand automatic selection
   from matching evidence rather than model-year guesses.

The broader goal is several capture adapters behind the same settings/layout/WLED
pipeline. It is not a promise that one TPK or one private Samsung interface can
cover every historical Tizen TV. The existing 15 fps measurements describe the
Q74T firmware and must not be generalized to other firmware families.
