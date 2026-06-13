# teinum-inrx-siusrank

[![Build and test](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mteinum/inrx-siusrank/actions/workflows/ci.yml)
[![Build and release](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml/badge.svg)](https://github.com/mteinum/inrx-siusrank/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Desktop app for creating SIUS Rank starter import CSV files from an inrX `storage.db3` SQLite database.

The app reads registrations and starter data from inrX, writes SIUS Rank import CSV files, keeps a stable `bib-map.csv`, supports SIUS Rank result writeback, and can generate ShootingSportsCloud setup files. The core project is a shared library; the Avalonia desktop app is the supported entrypoint.

## Related Links

- [inrX](https://inrx.org)
- [SIUS Rank](https://www.sius.com/en/product-page/siusrank)
- [SIUS Rank import/export settings](docs/siusrank-import-export-settings.md)
- [SSC integration notes](docs/ssc-integration.md)

## Run

From the repository root:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank.Desktop
```

The desktop app remembers selected paths and filters in a per-user `desktop-settings.json`.

## Main Workflows

- CSV export: creates one SIUS Rank import CSV per competition and updates `bib-map.csv`.
- Template setup: copies bundled SIUS Rank template XML files to the configured SIUS Rank templates directory.
- SIUS Rank writeback: previews or applies Rank List Main ODF XML results back to inrX.
- SSC setup: exports users, validates setup, and creates lane/reset JSON payloads.
- Diagnostics: read-only inspection for selected `Stevne.Id` values.

## CSV Export

The CSV uses the SIUS Rank starter import format:

```text
StartNumber;AccreditationNumber;IssfId;DisplayNameLong;DisplayName;FirstName;Name;BirthDay;Gender;Nation;BibNumber;TargetNumber;Relay;TeamIndex;DuellIndex;Groups;Comment;StarterId;TeamPosition;Team;TeamDisplay;TeamDuellIndex;TeamComment
```

Important mappings:

- `StartNumber`, `BibNumber`, and `StarterId` are stable championship numbers such as `26001`.
- `bib-map.csv` is read and written in the output directory so regenerated files keep existing numbers.
- Each competition CSV can contain multiple SIUS Rank `Groups` values.
- `Groups` is derived from effective KM/NM class, for example `Å -> Apen`, `V55 -> V55`, `Jm -> Jrm`, `SH-Åpen -> SH Å`.
- `Team` and `TeamDisplay` are filled with the club short name.

Example output files:

```text
20260706_Fri.csv
20260707_Silhuett.csv
20260708_Standard.csv
20260709_Fin.csv
20260709_Grov.csv
20260711_HurtigFin.csv
20260711_HurtigGrov.csv
```

## Configuration

The app loads `appsettings.json` from the current directory or executable directory.

```json
{
  "Paths": {
    "Inrx": "C:\\Program Files (x86)\\inrX",
    "SiusRankTemplates": "C:\\SIUS\\SiusRank\\Resources\\Templates"
  }
}
```

`Paths.Inrx` is used to find `storage.db3` as `<Inrx>\storage.db3` when no database path is selected in the UI. `Paths.SiusRankTemplates` is used to find `ShooterGroupsTemplate.xml` for group validation.

Bundled templates:

```text
Templates/ShooterGroupsTemplate.xml
Templates/ShootEventsTemplate2026_NM_Pistol.xml
Templates/ShootEventsTemplate2026_Approberte_Pistol.xml
```

## Build

Build and test:

```bash
dotnet build InrxToSiusRank/InrxToSiusRank.sln
dotnet test InrxToSiusRank/InrxToSiusRank.sln
```

Publish a local desktop package:

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

## License

MIT. See [LICENSE](LICENSE).
