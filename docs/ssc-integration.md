# ShootingSportsCloud integration

This document describes the deterministic NM2026/SIUS Rank to ShootingSportsCloud setup flow.

The current implementation only generates files:

- SSC users CSV
- SSC setup validation report
- SSC lane reset JSON
- SSC active-lanes JSON

It does not connect to SSC live APIs, MQTT, certificates, tokens, or Watchtower Range Live Results.

## NM2026 and SA951 note

NM2026 uses SIUS SA951 shooting monitors. SA951 monitors are not Watchtower AthleteMonitor clients.

`AthleteMonitorConnected=false` is the correct setting for this range and should not be changed to make Watchtower Range Live Results appear connected.

In Kanopus 2026.1.3, Watchtower Range Live Results reads viewdata from the `AthleteMonitor` source. On a SA951 range with no AthleteMonitor client, this can lead to `NullReferenceException` in `GetExerciseViewDataFromClient`. The SSC setup commands in this repository avoid that path entirely.

## Recommended match-day preparation

1. Generate SIUS Rank import CSV files first, so `siusrank-import/bib-map.csv` exists and contains the stable `26xxx` numbers.
2. Export SSC users from the same `storage.db3` and `bib-map.csv`.
3. Import `ssc-users.csv` manually in SSC.
4. Run `validate-ssc` against the imported users CSV before match day.
5. For each relay/startlag, generate lane reset and active-lanes JSON files.
6. Use the generated JSON as the candidate payload source for the manual SSC lane/reset setup process until the final API/MQTT contract is confirmed.

## Export users CSV

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  export-ssc-users \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --bib-map siusrank-import/bib-map.csv \
  --output ssc-setup/ssc-users.csv \
  --organization-name Legacy \
  --organization-id f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf
```

If `--output` is omitted, the command runs as a dry-run and prints counts and validation messages without writing the CSV or changing `bib-map.csv`.

The header is fixed to SSC's user import/export order:

```text
OrganizationName,OrganizationId,UserId,Name,FirstName,DisplayName,NationName,DisplayNationName,ISOCode,IOCCode,UserClassName,UserClassId,UserGroupName,UserGroupId,ShootingSportsCloudUserId,DateOfBirth,Gender,UserPictureId,UserPreferredLanguage
```

Mapping notes:

- `OrganizationName`: `Legacy`
- `OrganizationId`: `f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf`
- `UserId`: stable `26xxx` value from `bib-map.csv`
- `NationName` and `DisplayNationName`: `Norway` for blank/NO/NOR/Norge/Norway in inrX
- `ISOCode` and `IOCCode`: `NOR` for Norway
- `DateOfBirth`: `yyyy-MM-dd` only when inrX contains a full supported date
- `Gender`: `M` or `F` only when the inrX value maps safely

The default encoding is UTF-8 BOM. Use `--encoding windows-1252` only if SSC import on the target machine requires it.

## Validate SSC setup

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  validate-ssc \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --bib-map siusrank-import/bib-map.csv \
  --users-csv ssc-setup/ssc-users.csv
```

Validation checks:

- every selected starter has a matching SSC `UserId`
- duplicate `UserId` rows
- empty or invalid `OrganizationId`
- empty `OrganizationName`
- empty `Name`, `FirstName`, or `DisplayName`
- target/lane numbers outside `1..40` when a target exists
- SSC `ExerciseName` mapping for the selected inrX exercise

Hard errors return a non-zero exit code.

## Export lane/reset payloads

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  export-ssc-lanes \
  --db storage.db3 \
  --stevne-id 405 \
  --startlag "2026-07-06T09:00:00" \
  --bib-map siusrank-import/bib-map.csv \
  --output-dir ssc-setup/lanes \
  --lane-count 40
```

`--lane-count` supports `10`, `25`, and `40`; default is `40`.

The command writes:

- `ssc-lanes-reset-1-40.json`: all lanes `1..40`, inactive, for reset.
- `ssc-active-lanes-YYYYMMDDTHHMMSS.json`: only lanes with starters in the selected startlag.

JSON fields are camel-case and marked with:

```json
{
  "spec": "InrxToSiusRank.SSC.Lanes.v1",
  "warning": "Payload spec candidate..."
}
```

Active lane entries contain:

```json
{
  "lane": 1,
  "active": true,
  "userId": "26001",
  "displayName": "TEINUM Morten",
  "exerciseName": "50m Fripistol",
  "deltakerId": 100,
  "resultatId": 1001
}
```

Reset payloads include every lane, even lanes without a shooter.

## Manual SSC work that remains

- Import `ssc-users.csv` in SSC.
- Confirm SSC exercise names match the local SSC configuration.
- Decide the final SSC lane/reset API or MQTT payload contract.
- Add secrets, certificates, or credentials only when live forwarding is intentionally implemented.
- Do not use this app to change Watchtower/AthleteMonitor connectivity for SA951.
