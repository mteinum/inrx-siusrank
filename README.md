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

- `StartNumber`, `AccreditationNumber`, `BibNumber`, and `StarterId` are set to inrX `Resultat.Id`.
- KM/NM class is read from `Resultat.MklasseId1`.
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

`--stevne-ids` supports both comma-separated ids and ranges:

```text
405,406,407
405-411
405-407,409,411
```

Example output files:

```text
20260706_FP_Apen.csv
20260707_RFP_Apen.csv
20260707_RFP_NF_V55.csv
20260708_STP_M.csv
20260709_SPW_K.csv
20260709_SPW_Jk.csv
20260709_SPSH1_SH1-P3.csv
20260709_CFP_Apen.csv
20260711_SPRF_M.csv
20260711_CFPRF_Apen.csv
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
  --output-dir siusrank-import \
  --shooter-groups-template InrxToSiusRank/src/InrxToSiusRank/Templates/ShooterGroupsTemplate.xml
```

This does not change the export. It only stops the run if a `Groups` value is not found in the template file.

If `Paths.SiusRankTemplates` points to a directory containing `ShooterGroupsTemplate.xml`, validation is enabled automatically. Both XML templates are copied to `Templates/` next to the published executable when you run `dotnet publish`; `appsettings.json` is also copied next to the executable.
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
.\InrxToSiusRank.exe --wizard
```

File export on Windows:

```powershell
.\InrxToSiusRank.exe --db .\storage.db3 --stevne-ids 405-411 --output-dir .\siusrank-import
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
--settings <path>                   Path to appsettings.json.
--db <path>                         Path to storage.db3. Overrides appsettings.
--wizard                            Start interactive wizard.
--stevne-id <id>                    Select one Stevne.Id.
--stevne-ids <ids>                  Select several events, for example 405,406 or 405-411.
--event-date <yyyy-MM-dd>           Select event by date.
--event-name <text>                 Filter event by name together with --event-date.
--ovelse <name>                     Select exercise, for example Fripistol.
--ovelse-id <id>                    Select OvelseDef.Id.
--output-dir <path>                 Directory for generated CSV files.
--shooter-groups-template <path>    Validate Groups against SIUS Rank template.
--encoding <utf8-bom|windows-1252>  Encoding. Default: utf8-bom.
```

## Test

```bash
dotnet test InrxToSiusRank/InrxToSiusRank.sln
```

## License

MIT. See [LICENSE](LICENSE).
