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
- KM/NM-klasse
- Import method: file, clipboard, or both
- Optional validation against `ShooterGroupsTemplate.xml`

Direct export:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --output NM50FRI_APEN_import.csv
```

Validate the generated `Groups` values against the SIUS Rank shooter group template:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --output NM50FRI_APEN_import.csv --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

Copy the same import data directly to the clipboard:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --clipboard
```

Create a file and copy to clipboard in one run:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --output NM50FRI_APEN_import.csv --clipboard
```

Create one import file per KM/NM class for one event:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-id 405 --all-classes --output-dir siusrank-import --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

Create one import file per KM/NM class for several events:

```powershell
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --db storage.db3 --stevne-ids 405-411 --all-classes --output-dir siusrank-import --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

`--stevne-ids` accepts comma-separated ids and ranges, for example `405,406,407` or `405-411`. If `--ovelse`/`--ovelse-id` is omitted in bulk mode, the program uses the only exercise on each selected `Stevne`; if a `Stevne` has multiple exercises, it asks you to specify the exercise.

This creates a SIUS Rank import file with:

- Semicolon-delimited columns.
- The same header as `SiusRank_importExample.csv`.
- UTF-8 BOM and CRLF line endings by default.
- Starters filtered by inrX `KM/NM` class (`Resultat.MklasseId1`).
- KM/NM classes are mapped to the SIUS Rank shooter group names used by `ShooterGroupsTemplate.xml`, for example `Å -> Apen`, `M -> Menn`, `K -> Kvinner`, `Jm -> Jrm`, `Jk -> Jrk`, `V55 -> V55`.
- Bulk output file names use SIUS Rank event codes, for example `FP`, `STP`, `SPM`, `CFP`, `SPRF`, and `CFPRF`. Silhuett open class uses `RFP` because it has a final; the other Silhuett classes use `RFP_NF`.
- `StartNumber`, `AccreditationNumber`, `BibNumber`, and `StarterId` all preserve inrX `Resultat.Id`.
- `Team` and `TeamDisplay` are filled with the club short name by default. Use `--no-include-club-team` to leave them empty.

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
.\InrxToSiusRank.exe --db .\storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --output .\NM50FRI_APEN_import.csv
```

For SIUS Rank's "Update starters from clipboard":

```powershell
.\InrxToSiusRank.exe --db .\storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --clipboard
```

## Options

```text
--settings <path>                   Path to appsettings.json.
--db <path>                         Path to storage.db3. Overrides appsettings.
--wizard                            Start interactive wizard.
--stevne-id <id>                    inrX Stevne.Id. Use this or --event-date/--event-name.
--stevne-ids <ids>                  Bulk select stevner, for example 405,406,407 or 405-411.
--event-date <yyyy-MM-dd>           Select event by date.
--event-name <text>                 Select event by name text together with --event-date.
--ovelse <name>                     Exercise name, for example Fripistol.
--ovelse-id <id>                    Select by OvelseDef.Id.
--output <path>                     Output CSV path. Optional when --clipboard is used.
--output-dir <path>                 Output directory for --all-classes.
--clipboard                         Copy import data to clipboard.
--copy-to-clipboard                 Same as --clipboard.
--klasse <value>                    Filter by inrX KM/NM class, for example Å, V55, V65.
--km-nm-klasse <value>              Same as --klasse.
--all-classes                       Export one file per KM/NM class.
--sius-group <value>                Override SIUS Rank Groups value. Default: derive from KM/NM class.
--shooter-groups-template <path>    Validate Groups against SIUS Rank ShooterGroupsTemplate.xml.
--encoding <utf8-bom|windows-1252>  Output encoding. Default: utf8-bom.
--include-club-team                 Fill Team and TeamDisplay from club name. Default.
--no-include-club-team              Leave Team and TeamDisplay empty.
```
