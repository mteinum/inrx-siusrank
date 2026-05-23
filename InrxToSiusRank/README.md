# InrxToSiusRank

[![Build and test](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml)
[![Build and release](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml/badge.svg)](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)

Small .NET console application for exporting SIUS Rank starter import CSV files from an inrX `storage.db3` SQLite database.

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

`Paths.Inrx` is used to find `storage.db3` as `<Inrx>\storage.db3` when `--db` is not supplied. If your database is stored elsewhere, either pass `--db` or add `Paths.Database` to `appsettings.json`.

`Paths.SiusRankTemplates` is used to find `ShooterGroupsTemplate.xml` when `--shooter-groups-template` is not supplied. Command-line options override values from `appsettings.json`.

## Example

From the repository root:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --wizard
```

The interactive wizard lets you filter and select:

- Stevne
- Øvelse
- Output directory
- Optional validation against `ShooterGroupsTemplate.xml`

Create one import file per KM/NM class for one event:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --output-dir siusrank-import
```

Specify the exercise when a selected event has more than one exercise:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --ovelse Fripistol --output-dir siusrank-import
```

Validate the generated `Groups` values against the SIUS Rank shooter group template:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --output-dir siusrank-import --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

Create one import file per KM/NM class for several events:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-ids 405-411 --output-dir siusrank-import --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

`--stevne-ids` accepts comma-separated ids and ranges, for example `405,406,407` or `405-411`. If `--ovelse`/`--ovelse-id` is omitted, all exercises in the selected events are exported in the same run. This keeps the shared championship start and bib number sequence stable across the generated files.

This creates SIUS Rank import files with:

- Semicolon-delimited columns.
- The same header as `SiusRank_importExample.csv`.
- UTF-8 BOM and CRLF line endings by default.
- One output file per inrX `KM/NM` class (`Resultat.MklasseId1`).
- If `Resultat.MklasseId1` is missing or `-`, `Hurtig Grov`, `Grovpistol`, `Silhuett`, and `Fripistol` are exported as `Apen`; other exercises fall back to gender, with male shooters exported as class `M` and female shooters as class `K`.
- KM/NM classes are mapped to the SIUS Rank shooter group names used by `ShooterGroupsTemplate.xml`, for example `Å -> Apen`, `M -> Menn`, `K -> Kvinner`, `Jm -> Jrm`, `Jk -> Jrk`, `V55 -> V55`.
- Output file names use Norwegian class-specific SIUS Rank event codes, for example `Fri_V73`, `Standard_M`, `Fin_K`, `Fin_SH1-P3`, `Grov_Apen`, `HurtigFin_M`, and `HurtigGrov_V55`. The bundled shoot-event XML uses the same Norwegian event codes.
- `StartNumber`, `BibNumber`, and `StarterId` are assigned as championship numbers using the event year plus a shared shooter sequence, for example `26001` for 2026. The same `Deltaker.Id` receives the same number across all selected events. `AccreditationNumber` keeps the existing membership-number fallback behavior, using the assigned start number when no membership number exists.
- `Team` and `TeamDisplay` are filled with the club short name.

## SIUS Rank templates

The repository includes the SIUS Rank templates used for NM Pistol:

```text
InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
InrxToSiusRank/src/InrxToSiusRank/Templates/ShootEventsTemplate2026_NM_Pistol.xml
```

The standard location in a SIUS Rank Windows installation is:

```text
C:\SIUS\SiusRank\Resources\Templates
```

Both XML files are copied to the `Templates/` directory in `dotnet publish` output, and `appsettings.json` is copied next to the executable.
For use in SIUS Rank, copy the files to `C:\SIUS\SiusRank\Resources\Templates`.
If `Paths.SiusRankTemplates` points to this directory, `ShooterGroupsTemplate.xml` is used automatically for validation.
`ShootEventsTemplate2026_NM_Pistol.xml` contains class-specific NM shoot events, so SIUS Rank reports show the class in the event heading.

## Seed NM startlag

Preview NM startlag seeding without changing the database:

```powershell
.\InrxToSiusRank.exe seed-startlag --db .\storage.db3 --stevne-ids 405-411
```

Apply the planned changes:

```powershell
.\InrxToSiusRank.exe seed-startlag --db .\storage.db3 --stevne-ids 405-411 --apply
```

The command creates `storage.db3.bak-seed-YYYYMMDD-HHMMSS` before writing. It matches NSF ranking rows by `Deltaker.sa2Id == ranking.personId`, seeds `Å`, `M`, `K`, `Jr-NM`, `Jm`, and `Jk`, keeps classes as contiguous blocks, and keeps multi-shooter seed groups together. For non-Silhuett events, the seed group is placed at the latest point in its class block that avoids creating an underfilled startlag before it; remaining targets after a seed group can be filled by the same or next class. Silhuett keeps the requested seeded Å lag and uses target numbers `3, 8, 13, 18, 23, 28, 33`. Other 25m exercises use competition targets `1-35`; targets `36-38` are kept spare.

## Show NM timetable

Show the NM startlag timetable:

```powershell
.\InrxToSiusRank.exe show-timetable --db .\storage.db3
```

`show-timetable` defaults to NM `Stevne.Id` `405-411`. Use `--stevne-id` or `--stevne-ids` to narrow it:

```powershell
.\InrxToSiusRank.exe show-timetable --db .\storage.db3 --stevne-id 406
```

The output lists each NM event, target range, startlag time, shooter count versus capacity, and class mix. Finpistol and Grovpistol are shown as two-stage events: Precision on `2026-07-09` and Rapid on `2026-07-10`, with Finpistol before Grovpistol on both days. Startlag over target capacity are marked `OVER CAPACITY`.

## Windows executable

Build a self-contained Windows executable:

```powershell
dotnet publish InrxToSiusRank/src/InrxToSiusRank/InrxToSiusRank.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The executable is created at:

```text
InrxToSiusRank/src/InrxToSiusRank/bin/Release/net8.0/win-x64/publish/InrxToSiusRank.exe
```

Use `appsettings.json` for the database path, or pass `--db` with the full path:

```powershell
.\InrxToSiusRank.exe --wizard
```

Direct export:

```powershell
powershell -ExecutionPolicy Bypass -File .\sync.ps1
```

Override paths if needed:

```powershell
.\sync.ps1 -DatabasePath "C:\Users\ms\Dropbox\KPS-Stevne\INRX191\storage.db3" -StevneIds "413-417" -OutputDir ".\siusrank-import"
```

## Options

```text
--settings <path>                   Path to appsettings.json.
--db <path>                         Path to storage.db3. Overrides appsettings.
--wizard                            Start interactive wizard.
--stevne-id <id>                    inrX Stevne.Id. Use this or --event-date/--event-name.
--stevne-ids <ids>                  Bulk select stevner and export all exercises, for example 405,406,407 or 405-411.
--event-date <yyyy-MM-dd>           Select event by date.
--event-name <text>                 Select event by name text together with --event-date.
--ovelse <name>                     Exercise name, for example Fripistol.
--ovelse-id <id>                    Select by OvelseDef.Id.
--output-dir <path>                 Output directory for generated CSV files.
--shooter-groups-template <path>    Validate Groups against SIUS Rank ShooterGroupsTemplate.xml.
--encoding <utf8-bom|windows-1252>  Output encoding. Default: utf8-bom.
seed-startlag                       Preview or apply NM startlag seeding from NSF ranking.
--ranking-period-start <iso>        Ranking period start for seed-startlag.
--ranking-period-end <iso>          Ranking period end for seed-startlag.
--apply                             Write seed-startlag changes after creating a backup.
show-timetable                      Show NM timetable. Default Stevne.Id range: 405-411.
```
