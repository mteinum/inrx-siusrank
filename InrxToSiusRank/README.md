# InrxToSiusRank

Avalonia desktop app plus shared core library for SIUS Rank and inrX workflows.

Run from the repository root:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank.Desktop
```

The core `InrxToSiusRank` project is a library. The supported app entrypoint is `InrxToSiusRank.Desktop`.

## Workflows

- Export SIUS Rank starter import CSV files from inrX.
- Maintain stable championship numbers in `bib-map.csv`.
- Copy bundled SIUS Rank templates.
- Preview/apply SIUS Rank ODF result writeback to inrX.
- Generate and validate ShootingSportsCloud setup files.

## Build

```bash
dotnet build InrxToSiusRank.sln
dotnet test InrxToSiusRank.sln
```

Publish the desktop app:

```bash
dotnet publish src/InrxToSiusRank.Desktop/InrxToSiusRank.Desktop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Use `-r osx-arm64` on Apple Silicon Mac.
