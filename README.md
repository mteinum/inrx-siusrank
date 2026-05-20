# teinum-inrx-siusrank

[![Build and test](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml)
[![Build and release](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml/badge.svg)](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml)

CLI tool for creating SIUS Rank starter import files from an inrX `storage.db3` SQLite database.

The program reads registrations and starter data from inrX and writes CSV files that can be imported into SIUS Rank with:

- `Update starters from file`
- `Update starters from clipboard`

The tool does not create a separate SIUS Data start list. SIUS Rank creates the SIUS Data start list from the imported starters.

## Related Links

- [inrX](https://inrx.org)
- [SIUS Rank](https://www.sius.com/en/product-page/siusrank)

## Export Format

The CSV uses the SIUS Rank starter import format, with the same header style as `SiusRank_importExample.csv`:

```text
StartNumber;AccreditationNumber;IssfId;DisplayNameLong;DisplayName;FirstName;Name;BirthDay;Gender;Nation;BibNumber;TargetNumber;Relay;TeamIndex;DuellIndex;Groups;Comment;StarterId;TeamPosition;Team;TeamDisplay;TeamDuellIndex;TeamComment
```

Important mappings:

- `StartNumber`, `AccreditationNumber`, `BibNumber`, and `StarterId` are set to inrX `Resultat.Id`.
- KM/NM class is read from `Resultat.MklasseId1`.
- `Groups` is derived from KM/NM class, for example `Å -> Apen`, `V55 -> V55`, `Jm -> Jrm`.
- `Team` and `TeamDisplay` can be filled with the club short name by using `--include-club-team`.
- Names are kept at full length as shown in SIUS Rank.

## Interactive Run

From the repository root:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --wizard --db storage.db3
```

The wizard lets you select the event, exercise, KM/NM class, and whether the import data should be written to a file, copied to the clipboard, or both.

## Create One Import File

Example for Fripistol, KM/NM class `Å`:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --ovelse Fripistol \
  --klasse Å \
  --output NM50FRI_APEN_import.csv \
  --include-club-team
```

Copy to clipboard instead of writing a file:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --ovelse Fripistol \
  --klasse Å \
  --clipboard \
  --include-club-team
```

## Create Import Files For All Classes

One file per KM/NM class for one event:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --all-classes \
  --output-dir siusrank-import \
  --include-club-team
```

One file per KM/NM class for several events:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --all-classes \
  --output-dir siusrank-import \
  --include-club-team
```

`--stevne-ids` supports both comma-separated ids and ranges:

```text
405,406,407
405-411
405-407,409,411
```

Example output files:

```text
20260706_Fri_Apen.csv
20260706_Fri_V55.csv
20260706_Fri_V65.csv
20260706_Fri_V73.csv
20260706_Fri_SH1-P4.csv
```

## Validate Shooter Groups

The repository includes the SIUS Rank templates:

```text
InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
InrxToSiusRank/src/InrxToSiusRank/Templates/ShootEventsTemplate2026_NM_Pistol.xml
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
  --all-classes \
  --output-dir siusrank-import \
  --include-club-team \
  --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

This does not change the export. It only stops the run if a `Groups` value is not found in the template file.

Both XML templates are copied to `Templates/` next to the published executable when you run `dotnet publish`.
For use in SIUS Rank, copy the files to `C:\SIUS\SiusRank\Resources\Templates`.

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
.\InrxToSiusRank.exe --wizard --db .\storage.db3
```

Bulk export on Windows:

```powershell
.\InrxToSiusRank.exe --db .\storage.db3 --stevne-ids 405-411 --all-classes --output-dir .\siusrank-import --include-club-team
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

The workflow runs tests, builds self-contained single-file binaries, and publishes release assets for:

```text
win-x64
osx-x64
osx-arm64
linux-x64
```

## Options

```text
--db <path>                         Path to storage.db3.
--wizard                            Start interactive wizard.
--stevne-id <id>                    Select one Stevne.Id.
--stevne-ids <ids>                  Select several events, for example 405,406 or 405-411.
--event-date <yyyy-MM-dd>           Select event by date.
--event-name <text>                 Filter event by name together with --event-date.
--ovelse <name>                     Select exercise, for example Fripistol.
--ovelse-id <id>                    Select OvelseDef.Id.
--klasse <value>                    Select KM/NM class, for example Å, V55, V65.
--km-nm-klasse <value>              Same as --klasse.
--all-classes                       Create one file per KM/NM class.
--output <path>                     File path for normal export.
--output-dir <path>                 Directory for --all-classes.
--clipboard                         Copy import data to clipboard.
--copy-to-clipboard                 Same as --clipboard.
--include-club-team                 Fill Team and TeamDisplay with club short name.
--sius-group <value>                Override Groups for normal export.
--shooter-groups-template <path>    Validate Groups against SIUS Rank template.
--encoding <utf8-bom|windows-1252>  Encoding. Default: utf8-bom.
```

## Test

```bash
dotnet test InrxToSiusRank/InrxToSiusRank.sln
```
