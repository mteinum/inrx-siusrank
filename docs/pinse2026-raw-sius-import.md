# Pinse2026 Raw SIUS Data Import

This documents the one-off repair/import flow used for Pinse2026 day 1 after
some SIUS Rank entries had bib numbers that were too large and could not be
matched back to results.

The repair job was local to `Stevner/Pinse2026`. It was not added to the
InrxToSiusRank product.

## Inputs

- `Stevner/Pinse2026/storage.db3`: latest inrX database.
- `Stevner/Pinse2026/siusrank-import/bib-map.csv`: persistent mapping from
  inrX/NSF identity to generated SIUS bib number.
- `Stevner/Pinse2026/Rank_A/Pinse 2026.srkl` and
  `Stevner/Pinse2026/Rank_B/Pinse 2026.srkl`: SIUS Rank databases used during
  the event.
- `Stevner/Pinse2026/SiusData/.../*.csv`: raw SIUS shot log files.
- `Stevner/Pinse2026/SiusData/.../*_mod.csv`: SIUS modification files used to
  identify ignored or edited raw shot log entries.
- `Stevner/Pinse2026/SiusData/.../*_stl.csv`: SIUS start list exports, used for
  manual control only in this repair.

The target events for result repair were:

- `HurtigFin_K`
- `HurtigFin_M`
- `HurtigGrov_Apen`

`Standard_K` and `Standard_M` were used only as controls for bib mapping and
duplicate checks.

## Bib Mapping

SIUS Rank could not safely use the original NSF ids as start/bib numbers because
some values exceeded the 6 digit limit. The repair therefore used generated bib
numbers in the `26nnn` range, where `26` is the event year prefix and `nnn` is a
stable sequence number.

The mapping was kept in:

```text
Stevner/Pinse2026/siusrank-import/bib-map.csv
```

The file columns were:

```text
nsfId,bibNumber,deltakerId,name,source
```

Mapping sources were applied in this order:

1. Existing `bib-map.csv`.
2. Existing generated SIUS import CSV files in `siusrank-import`.
3. Existing `26nnn` entries already present in `Rank_A` or `Rank_B`.
4. Newly allocated `26nnn` numbers for shooters without mapping.

The local repair script identified shooters from `storage.db3` for the Pinse2026
stevner and matched old values using:

- original NSF id
- generated bib number
- inrX `Resultat.Id`
- last 6 digits of long NSF ids
- unique normalized display name, as a fallback/control

## Raw SIUS Shot Import

Raw SIUS shot rows were grouped by the old SIUS start number in the raw `.csv`
file. The old start number was mapped back to the shooter through the mapping
logic above.

For each shooter/event:

1. Read raw rows from the event raw SIUS `.csv`.
2. Read ignored/edited log ids from the matching `_mod.csv`.
3. Split rows into sighting rows and competition rows.
4. Keep the first 5 sighting rows for display/history.
5. Keep all competition rows in the `Shot` table, but exclude `_mod.csv` ignored
   log ids from scoring.
6. Require exactly 60 counted competition shots before building a result package.
7. Calculate:
   - total integer score
   - decimal score total
   - inner tens
   - six 10-shot series
   - three 20-shot stage subtotals
   - `ShotByShotAsText`
   - `ScoreValuesCountedAsText`

For `HurtigFin_M`, raw data from `Hurtig Grov` was also allowed as an additional
source because some shooters/results were present there after the parallel SIUS
Rank setup.

## SIUS Rank Database Updates

For each repaired assignment in each `.srkl` database, the repair updated:

- `Entry.StartNumber`
- `EntryAssignment.CompetitionBibNumber`
- `EntryAssignment.StarterId`
- `EntryAssignment.SiusDataStartNumber`
- `EntryAssignment` result/ranking fields
- `ScoreContainer` total and grand total
- six series `ScoreContainer` rows
- three stage `ScoreContainer` rows
- `Shot` rows
- `IndividualResult` rank rows
- start list drawing data

When replacing shot rows, it was not enough to insert into `Shot`. `Shot`
inherits from SIUS Rank's `XpoNode`, so every inserted shot also needed a
matching `XpoNode` row with `ObjectType = 17` (`Sius.Rank.Shot`).

Missing `XpoNode` rows caused this SIUS Rank symptom:

- totals and series could appear in the main grid
- expanding the row showed `Record 0 of 0` for shots
- pressing `Rank List Main` could clear results because SIUS Rank could not load
  `EntryAssignment.AllShots`

The final repair explicitly ensured:

- every `Shot` row had a matching `XpoNode`
- every `Shot.ShotId` was a valid GUID
- `DateTimeStampFromMaster` was `NULL`, not an empty string
- `LogEventId` was normalized to `0` for repaired rows

## Result Source Priority

When filling a missing result, the script preferred:

1. A complete result already present in the other SIUS Rank database.
2. A result rebuilt from raw SIUS shot data.

Existing conflicting complete results between `Rank_A` and `Rank_B` were reported
but not overwritten automatically.

Known non-starters in `HurtigGrov_Apen` were left without results by design.

## Validation

The repair script defaulted to dry-run. The write mode was explicit:

```sh
python3 Stevner/Pinse2026/_repair/repair_day1.py
python3 Stevner/Pinse2026/_repair/repair_day1.py --apply
```

After applying, validation checked:

- `PRAGMA integrity_check` returned `ok`.
- No active repaired event entries used old NSF/start numbers.
- No duplicate active assignment per event/shooter.
- Missing results before/after.
- `shots_missing_xponode = 0`.
- No blank `ShotId` values.
- Expected 60 counted competition shots per completed starter.
- SIUS Rank UI showed series and expandable shot rows.
- SIUS Rank reports (`Rank List Main`) could be generated without clearing
  repaired results.

When files were copied to the SIUS Rank Windows machine, `Get-FileHash` was used
to verify that the opened file matched the repaired copy before testing in SIUS
Rank.

## Git Policy

The event data and repair workspace are local artifacts and should not be
committed:

- `Stevner/`
- `.srkl` databases and backups
- `.db3` databases
- SIUS raw/export CSV files
- generated `siusrank-import` CSV files
- copied check files from the SIUS Rank machine

The reusable product changes that should be committed are the InrxToSiusRank
changes for persistent `bib-map.csv` handling, tests, README updates, this
documentation, and `.gitignore` rules that keep local event data out of git.
