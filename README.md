# teinum-inrx-siusrank

[![Build and test](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml)
[![Build and release](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml/badge.svg)](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

CLI tool for creating SIUS Rank starter import files from an inrX `storage.db3` SQLite database.

The program reads registrations and starter data from inrX and writes CSV files that can be imported into SIUS Rank with `Update starters from file`.

The tool does not create a separate SIUS Data start list. SIUS Rank creates the SIUS Data start list from the imported starters.

## Related Links

- [inrX](https://inrx.org)
- [SIUS Rank](https://www.sius.com/en/product-page/siusrank)

## Configuration

The application loads `appsettings.json` from the current directory or from the executable directory. You can also pass a specific file with `--settings`.

Default `appsettings.json`:

```json
{
  "Paths": {
    "Inrx": "C:\\Program Files (x86)\\inrX",
    "SiusRankTemplates": "C:\\SIUS\\SiusRank\\Resources\\Templates"
  }
}
```

`Paths.Inrx` is used to find `storage.db3` as `<Inrx>\storage.db3` when `--db` is not supplied. If your database is somewhere else, either pass `--db` or add `Paths.Database` to `appsettings.json`.

`Paths.SiusRankTemplates` is used to find `ShooterGroupsTemplate.xml` when `--shooter-groups-template` is not supplied. Command-line options override values from `appsettings.json`.

## Export Format

The CSV uses the SIUS Rank starter import format, with the same header style as `SiusRank_importExample.csv`:

```text
StartNumber;AccreditationNumber;IssfId;DisplayNameLong;DisplayName;FirstName;Name;BirthDay;Gender;Nation;BibNumber;TargetNumber;Relay;TeamIndex;DuellIndex;Groups;Comment;StarterId;TeamPosition;Team;TeamDisplay;TeamDuellIndex;TeamComment
```

Important mappings:

- `StartNumber`, `BibNumber`, and `StarterId` are assigned as championship numbers using the event year plus a shared shooter sequence, for example `26001` for 2026. The same `Deltaker.Id` receives the same number across all selected events. The exporter reads and writes `bib-map.csv` in the output directory so regenerated files keep existing `Deltaker.Id` to bib mappings and only allocate new numbers for new shooters. `nsfId` is stored in the map for control and traceability. `AccreditationNumber` keeps the existing membership-number fallback behavior, using the assigned start number when no membership number exists.
- KM/NM class is read from `Resultat.MklasseId1`. If that class is missing or `-`, `Hurtig Grov`, `Grovpistol`, `Silhuett`, and `Fripistol` are exported as `Apen`; other exercises fall back to gender, with male shooters exported as class `M` and female shooters as class `K`.
- `Groups` is derived from KM/NM class, for example `Å -> Apen`, `V55 -> V55`, `Jm -> Jrm`.
- `Team` and `TeamDisplay` are filled with the club short name.
- Names are kept at full length as shown in SIUS Rank.

## Interactive Run

From the repository root:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --wizard
```

The wizard lets you select the event and exercise, then writes one import file per KM/NM class. Use `--db storage.db3` if the database is not available through `appsettings.json`.

## Create Import Files

One file per KM/NM class for one event:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --output-dir siusrank-import
```

Specify the exercise when a selected event has more than one exercise:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --ovelse Fripistol \
  --output-dir siusrank-import
```

One file per KM/NM class for several events:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --output-dir siusrank-import
```

When `--stevne-ids` is used without `--ovelse` or `--ovelse-id`, all exercises in the selected events are exported in the same run. This keeps the `26nnn` start and bib number sequence shared across the generated files.

`--stevne-ids` supports both comma-separated ids and ranges:

```text
405,406,407
405-411
405-407,409,411
```

Example output files:

```text
20260706_Fri_Apen.csv
20260706_Fri_SH1-P4.csv
20260707_Silhuett_Apen.csv
20260707_Silhuett_Jr-NM.csv
20260707_Silhuett_V55.csv
20260708_Standard_M.csv
20260709_Fin_K.csv
20260709_Fin_Jk.csv
20260709_Fin_SH1-P3.csv
20260709_Grov_Apen.csv
20260711_HurtigFin_M.csv
20260711_HurtigGrov_Apen.csv
```

## Validate Shooter Groups

The repository includes the SIUS Rank templates:

```text
InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
InrxToSiusRank/src/InrxToSiusRank/Templates/ShootEventsTemplate2026_NM_Pistol.xml
InrxToSiusRank/src/InrxToSiusRank/Templates/ShootEventsTemplate2026_Approberte_Pistol.xml
```

The standard location in a SIUS Rank Windows installation is:

```text
C:\SIUS\SiusRank\Resources\Templates
```

`ShooterGroupsTemplate.xml` can be used to validate that the exported `Groups` values exist in the SIUS Rank setup:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --output-dir siusrank-import \
  --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

This does not change the export. It only stops the run if a `Groups` value is not found in the template file.

If `Paths.SiusRankTemplates` points to a directory containing `ShooterGroupsTemplate.xml`, validation is enabled automatically. The XML template files are copied to `Templates/` next to the published executable when you run `dotnet publish`; `appsettings.json` is also copied next to the executable.
For use in SIUS Rank, copy the files to `C:\SIUS\SiusRank\Resources\Templates`.
`ShootEventsTemplate2026_NM_Pistol.xml` contains class-specific NM shoot events such as `Fri_V73`,
`Standard_M`, and `Fri_SH1-P4`, so SIUS Rank reports show the class in the event heading.
`ShootEventsTemplate2026_Approberte_Pistol.xml` contains normal approberte 25m/50m pistol events for the NSF 2026 classes, excluding 25m NAIS Fin/Grov.

## Seed NM Startlag

Preview NM startlag seeding from the NSF ranking API without changing the database:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  seed-startlag \
  --db storage.db3 \
  --stevne-ids 405-411
```

The command uses the 2026 ranking period by default:

```text
2025-12-31T23:00:00.000Z - 2026-12-31T22:59:59.999Z
```

Override it with `--ranking-period-start` and `--ranking-period-end`.

Apply the planned changes to `storage.db3`:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  seed-startlag \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --apply
```

Before writing, the command creates a backup named:

```text
storage.db3.bak-seed-YYYYMMDD-HHMMSS
```

Seeding matches NSF ranking rows by `Deltaker.sa2Id == ranking.personId`. Eligible seeded classes are `Å`, `M`, `K`, `Jr-NM`, `Jm`, and `Jk`. Classes stay as contiguous blocks. Multi-shooter seed groups stay together. For non-Silhuett events, the seed group is placed at the latest point in its class block that avoids creating an underfilled startlag before it; remaining targets after a seed group can be filled by the same or next class. Silhuett/RFP uses side targets `2, 4, 7, 9, 12, 14, 17, 19, 22, 24, 27, 29, 32, 34` so SIUS Rank can import two shooters per skivestativ with V/H filters. Other 25m exercises use competition targets `1-35`; targets `36-38` are kept spare.

## Show NM Timetable

Show the NM startlag timetable from `storage.db3`:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  show-timetable \
  --db storage.db3
```

`show-timetable` defaults to NM `Stevne.Id` `405-411`. Use `--stevne-id` or `--stevne-ids` to narrow it:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  show-timetable \
  --db storage.db3 \
  --stevne-id 406
```

The output lists each NM event, configured target range, startlag time, shooter count versus capacity, and class mix. Finpistol and Grovpistol are shown as two-stage events: Precision on `2026-07-09` and Rapid on `2026-07-10`, with Finpistol before Grovpistol on both days. Startlag over target capacity are marked `OVER CAPACITY`.

## Write SIUS Rank Results Back to inrX

After results are calculated in SIUS Rank, press `Rank List Main` for the relevant event(s). SIUS Rank writes ODF XML files under its `Exports` directory. The `writeback-siusrank` command reads those exported `IndividualResults` XML files and writes completed results back to inrX `Resultat` rows.

Preview without changing `storage.db3`:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  writeback-siusrank \
  --db storage.db3 \
  --stevne-ids 413-417 \
  --exports Rank_A/Exports \
  --bib-map siusrank-import/bib-map.csv
```

Apply the updates:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  writeback-siusrank \
  --db storage.db3 \
  --stevne-ids 413-417 \
  --exports Rank_A/Exports \
  --bib-map siusrank-import/bib-map.csv \
  --apply
```

Before writing, the command creates a backup named:

```text
storage.db3.bak-siusrank-writeback-YYYYMMDD-HHMMSS
```

Use `--event` to limit the import to one or more SIUS Rank event names:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  writeback-siusrank \
  --db storage.db3 \
  --stevne-id 413 \
  --exports Rank_A/Exports \
  --bib-map siusrank-import/bib-map.csv \
  --event HurtigFin_M,HurtigFin_K
```

Matching is done by `bib-map.csv` first, then by old inrX result id, NSF/accreditation number, and finally by unique name. Rows without a complete exported result with shots are skipped. SIUS Rank ODF exports contain total inner tens but not a per-shot inner-ten flag, so the writeback reconstructs the per-shot `O` markers by assigning the closest exported 10s until the exported inner-ten total is reached.

## Desktop UI

An Avalonia desktop app is available for Mac and Windows testing. It wraps the same export and writeback code as the CLI and provides:

- CSV export with `bib-map.csv` reuse.
- SIUS Rank writeback dry-run and apply.
- Read-only database diagnostics for selected `Stevne.Id` values.

The desktop app remembers selected paths and filters in a per-user `desktop-settings.json`, so `storage.db3`, output directory, shooter groups XML, exports directory, and related fields are restored on the next launch.

Run it from the repository root:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank.Desktop
```

Build a local self-contained desktop package:

```bash
dotnet publish InrxToSiusRank/src/InrxToSiusRank.Desktop/InrxToSiusRank.Desktop.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  /p:PublishSingleFile=true
```

Use `-r win-x64` for Windows. Release assets are named like:

```text
InrxToSiusRank.Desktop-v0.7.8-win-x64.zip
InrxToSiusRank.Desktop-v0.7.8-osx-arm64.tar.gz
```

## Build Windows Exe

Create a self-contained Windows executable:

```bash
dotnet publish InrxToSiusRank/src/InrxToSiusRank/InrxToSiusRank.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true
```

The executable is written to:

```text
InrxToSiusRank/src/InrxToSiusRank/bin/Release/net8.0/win-x64/publish/InrxToSiusRank.exe
```

On Windows:

```powershell
.\InrxToSiusRank.exe --wizard
```

File export on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\sync.ps1
```

Override paths if needed:

```powershell
.\sync.ps1 -DatabasePath "C:\Users\ms\Dropbox\KPS-Stevne\INRX191\storage.db3" -StevneIds "413-417" -OutputDir ".\siusrank-import"
```

## Create GitHub Release

The `.github/workflows/release.yml` workflow can be run manually from GitHub Actions with a version, for example `v0.1.0`.

Using GitHub CLI:

```bash
gh workflow run release.yml -f version=v0.1.0
```

You can also create a release by pushing a tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The workflow runs tests, then publishes both CLI and Avalonia desktop release assets for:

```text
win-x64
osx-x64
osx-arm64
linux-x64
```

CLI assets are named `InrxToSiusRank-<version>-<rid>.*`. Desktop assets are named `InrxToSiusRank.Desktop-<version>-<rid>.*`.

## Options

```text
--settings <path>                   Path to appsettings.json.
--db <path>                         Path to storage.db3. Overrides appsettings.
--wizard                            Start interactive wizard.
--stevne-id <id>                    Select one Stevne.Id.
--stevne-ids <ids>                  Select several events and export all exercises, for example 405,406 or 405-411.
--event-date <yyyy-MM-dd>           Select event by date.
--event-name <text>                 Filter event by name together with --event-date.
--ovelse <name>                     Select exercise, for example Fripistol.
--ovelse-id <id>                    Select OvelseDef.Id.
--output-dir <path>                 Directory for generated CSV files.
--shooter-groups-template <path>    Validate Groups against SIUS Rank template.
--encoding <utf8-bom|windows-1252>  Encoding. Default: utf8-bom.
seed-startlag                       Preview or apply NM startlag seeding from NSF ranking.
--ranking-period-start <iso>        Ranking period start for seed-startlag.
--ranking-period-end <iso>          Ranking period end for seed-startlag.
--apply                             Write seed-startlag changes after creating a backup.
show-timetable                      Show NM timetable. Default Stevne.Id range: 405-411.
writeback-siusrank                  Preview or apply SIUS Rank Rank List Main ODF XML results back to inrX.
--exports <path>                    SIUS Rank Exports directory for writeback-siusrank.
--bib-map <path>                    Optional bib-map.csv for writeback-siusrank.
--event <name>                      Optional comma-separated SIUS event filter for writeback-siusrank.
```

## Test

```bash
dotnet test InrxToSiusRank/InrxToSiusRank.sln
```

## License

MIT. See [LICENSE](LICENSE).
