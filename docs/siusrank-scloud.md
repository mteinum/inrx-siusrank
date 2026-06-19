# SIUScloud / SCloud in SiusRank

This document describes the SIUScloud integration found by decompiling the binaries under `./SiusRank`.

Main assemblies involved:

- `SiusRank.exe`: GUI entry points, token loading, SIUScloud selector/import dialog, maintenance dialog.
- `Sius.Rank.dll`: championship, shoot event, phase, entry, and document model hooks.
- `Sius.Rank.Common.dll`: `Sius.Rank.Converters.SCloudConverter`, which maps SiusRank objects to SIUScloud DTOs.
- `Sius.Rank.Exporter.Contracts.dll`: `Sius.Rank.Exporter.Contracts.SCloudExporter`, the export facade used by the application.
- `SIUScloud.DataStorage.dll`: cloud payload DTOs and identifiers.
- `SIUScloud.Gateway.dll`: gRPC/web API gateway, token parsing, upload implementation.
- `SIUScloud.Api.dll`: generated REST clients used for cloud import/maintenance.
- `SiusCloud.Common.dll`: shared cloud environment URI helpers.

## Terminology

The code uses both `SCloud` and `SIUScloud` names:

- `SCloud` is the short internal name used for settings, model properties, and exporter classes.
- `SIUScloud` is the user-facing cloud product name and namespace used by the gateway/API assemblies.

## Championship Fields

These are stored on `Sius.Rank.Championship` unless marked otherwise.

| Field | Purpose |
| --- | --- |
| `SIUScloudTenantId` | Tenant/organization GUID imported from SIUScloud competition details and sent in `CompetitionIdentifier`. |
| `SIUScloudPlannedId` | Planned competition GUID imported from SIUScloud. |
| `SIUScloudRunningId` | Running competition GUID. In the local model this is the championship object's `UniqueIdentifier`. |
| `CloudName` | Competition name sent to SIUScloud. If empty when a database is opened, it is initialized from `Championship.Name`. |
| `SCloudToken` | Base64-encoded JSON token bundle containing `Token` and `RefreshToken`. Size-limited to 4096 characters. |
| `SCloudResultApi` | Result API base URL used for gRPC and REST calls. It is derived from the token when loading a token, but can be overridden in the championship SCloud tab. |
| `SCloudExportEnabled` | Non-persistent runtime toggle. It is reset to `false` when a championship is opened and when the SCloud token is removed. |

`SCloudExportEnabled` is not saved as a persistent property; it must be enabled for the current session after a valid token/license is available.

## Phase Settings

These settings live on `Sius.Rank.Phase`, are loaded from shoot event templates, and are edited in the phase `Settings` tab.

| UI label | Phase property | Template XML | Cloud payload use |
| --- | --- | --- | --- |
| `Individual SCloud Face` | `SCloudFace` | `<SCloudFace>` | Sent as `SubEventResultSeriesPayload.SCloudFace`. |
| `Team SCloud Face` | `TeamSCloudFace` | `<TeamSCloudFace>` | Sent as `SubEventResultSeriesPayload.TeamSCloudFace`. |
| `Target picture is hidden` | `TargetPictureHidden` | `<TargetPictureHidden>` | Sent as `SubEventResultSeriesPayload.TargetPictureHidden`. |

Supported enum values:

- `SCloudFaceType`: `Default`, `Hidden`, `ByTotal`, `ByPoints`.
- `TeamSCloudFaceType`: `Default`, `Hidden`, `ByTeamTotal`, `ByTeamPoints`, `ByDuelPoints`.

Active templates mostly use:

- `SCloudFace`: usually `Default`.
- `TeamSCloudFace`: usually `Default`, with `ByTeamPoints` in some mixed/team templates.
- `TargetPictureHidden`: `true` for many shotgun templates, `false` for most rifle/pistol templates including `ShootEventsTemplate2026_NM_Pistol.xml`.

The phase editor populates these combo boxes directly from the enum values and writes changes back immediately to the selected phase.

## Token Handling

The toolbar/menu action `Load SCloud Token` opens a file dialog and reads the selected file as plain text. That text is treated as a JWT access token and wrapped into a `CompoundToken`, then into a `ShootingSportsCloudToken`, then stored on the championship as a base64 JSON string:

```json
{
  "Token": "...jwt...",
  "RefreshToken": "..."
}
```

Token behavior:

- Empty file: warning `S-Cloud token is empty.`
- Unreadable token: warning `S-Cloud token not readable.`
- Valid token: stored in `Championship.SCloudToken`; license is read from the token.
- API URL: derived by `Token.GetUrl(...)`.
- Expired token with refresh token: `RefreshSCloudToken(...)` posts `{"refreshToken":"..."}` to `Settings.Default.RefreshTokenUri`.
- Invalid token: export is disabled and the user gets a warning.

`Token.GetUrl(...)` derives the result API URL from JWT issuer/audience:

- If issuer contains `shootingsportscloud.com`: `https://{issuer}:8594/`.
- Else if an audience contains `shootingsportscloud.com`: `https://{audience}`.
- Fallback: `https://shootingsportscloud.com:8594/`.

For SIUScloud import through `SimpleFedappApi`, SiusRank uses the same base URL but replaces port `8594` with `8596`.

## UI Entry Points

SCloud controls in `FormSiusRank`:

- `mBarCheckItemSCloudExportEnabled`: toolbar toggle for live/export behavior.
- `mBarButtonItemLoadSCloudToken`: loads the token file.
- `mBarButtonItemReadSCloudData`: opens `SIUScloudSelector`.
- `barButtonSCloudMaintenance`: opens `SCloudMaintenance`.

The export toggle is enabled only when:

- the license type is not `None` or `Basic`;
- the SCloud token is valid and not expired.

When enabled, SiusRank starts a token refresh timer and subscribes to `SCloudExporter.FatalCloudErrorOccured`. A fatal cloud error adds a warning and unchecks the export toggle.

The championship editor has:

- an SCloud tab with a cloud API URL textbox and a button to set/update `SCloudResultApi`;
- a button to open SCloud maintenance;
- a licenses tab showing the SCloud token read-only;
- a remove SCloud token button, which disables export, clears `SCloudToken`, clears `SCloudResultApi`, clears license validation, and refreshes controls.

## Import From SIUScloud

`SIUScloudSelector` is titled `SIUScloud Championship Selector`.

Buttons and options:

- `Load Championships`: calls `SimpleFedappApi.AllCompetitionsAsync()`, ordered by `StartDate` descending.
- `Import Data`: imports selected data from the chosen cloud competition.
- Checkboxes under selected championship:
  - `Infos`: import competition details and logos.
  - `Structure`: create/update shoot events from SIUScloud events.
  - `Schedule`: apply schedule start times to phases/relays/stages.
  - `Entries`: import starters and assign them to events.
- `Export Competition to SIUScloud`: pushes the local competition to SIUScloud when `SCloudExportEnabled` is true.

Imported championship info:

- `SIUScloudTenantId`
- `SIUScloudPlannedId`
- `CloudName`
- `Location`
- `Country`
- `TimeZone`
- `StartDateTime`
- `EndDateTime`
- `Disciplines`
- header/footer logos

Imported structure:

- Calls `AllEvents(plannedCompetitionId)`.
- Finds a local `ShootEventConfiguration` by `eventDto.Code`.
- Creates the shoot event if missing.
- Sets `shootEvent.SIUScloudPlannedId`.
- Sets `shootEvent.PreEventTraining = true` if the cloud schedule contains a name with `Pre`.

Imported schedule:

- Calls `GetEventSchedule(eventPlannedId)`.
- Matches cloud schedule names to phase names after normalizing to lowercase alphanumeric text.
- Applies start times to phases, relays, or stages.

Imported entries:

- Calls `AllStarters(plannedCompetitionId)`.
- Matches entries by tenant, planned ID, and identifier.
- Creates or updates entries with last name, first name, nation, date of birth, gender, and bib/start number.
- If no positive bib is provided, assigns local start numbers from `9001`.
- Calls `GetEventStarters(eventPlannedId)` and creates event assignments.
- Uses `starter.TeamName ?? starter.TeamCountry` as the team string.
- Adds `RPO` to the shooter group when `starter.ShooterGroup` contains `RPO`.
- Adds `JuniorRecord` to the group for juniors in non-junior event codes.
- For mixed gender team events, assigns `TeamIndex` from gender and clay/non-clay rules.

## Export To SIUScloud

There are two export paths:

1. Live/session export, controlled by `SCloudExportEnabled` and document/model hooks.
2. Manual full competition export from `SIUScloudSelector` using `Export Competition to SIUScloud`.

`SCloudExporter` wraps the lower-level `LibraryResultApi` gRPC-web client. It sends a JWT bearer token on each gRPC call and uploads JSON or binary payloads.

Exported payload categories:

| Method | Payload | Gateway file type |
| --- | --- | --- |
| `DeliverCompetitionMetaInfoAsync` | `CompetitionMetaInfoPayload` | `MetaInfo` |
| `DeliverCompetitionEventMetaInfoAsync` | `CompetitionEventMetaInfoPayload` | `MetaInfo` |
| `DeliverSubEventMetaInfoAsync` | `SubEventMetaInfoPayload` | `MetaInfo` |
| `DeliverSubEventStartlistAsync` | `SubEventStartlistPayload` | `StartLists` |
| `DeliverAthletsAsync` | `AthleteMetaInfoPayload` | `MetaInfo` |
| `DeliverShotGroupsAsync` | `ShotGroupPayload` | `ShotGroups` |
| `DeliverResultsAsync` | `SubEventResultsPayload` | `Results` |
| `DeliverResultSeriesAsync` | `SubEventResultSeriesPayload` | `Series` |
| `DeliverExerciseResultDataAsync` | `SiusRankExerciseResultDataPayload` | `Shots` |
| `DeliverRecordsAsync` | `SiusRankRecordPayload` | `Records` |
| `DeliverBinaryDataAsync` | `BinaryDataPayload` plus bytes | binary upload |

Fatal vs non-fatal errors:

- Competition, event, subevent, startlist, and athlete meta upload errors trigger `FatalCloudErrorOccured`.
- Shot groups, results, series, exercise result data, records, and binary upload errors are warnings.

## What Is Exported

Competition metadata contains:

- identifiers: tenant, planned, running;
- `CloudName`, city, country;
- UTC start/end dates and IANA time zone;
- extensions `DocumentVersionControlEnabled` and `UsePictureHeader`;
- competition type from `IssfCode`.

Competition event metadata contains:

- event identifiers;
- event type with name, short name, event code, range;
- gender, junior flag, team flag, shotgun flag;
- start date;
- state derived from phase progress status.

Subevent metadata contains:

- phase or relay running ID;
- order;
- name;
- default target code;
- shooter groups for cloud export;
- start date;
- state;
- hidden flag. Pre-event training phases are hidden; elimination phases are hidden unless they produce team results.

Athlete metadata contains:

- cloud athlete identifiers;
- bib/start number;
- prefix, first name, last name;
- birthday/date of birth;
- gender;
- nation.

Startlist payloads contain one `SubEventStartlistEntry` per assignment:

- bib number;
- relay;
- firing lane;
- athlete with identifier, prefix, names, gender, birthday, bib, nation, and shooter groups.

Important: SCloud startlist payloads include date of birth (`Athlete.Birthday = Entry.BirthDay`). Local report templates can hide DOB, but SIUScloud export still sends DOB unless the export code is changed.

Results payloads contain:

- individual totals in `TotalResultsIndividual`;
- team-of-individuals totals in `TotalResultsTeamOfIndividuals`;
- rank display text;
- total score display;
- nation;
- remarks/extensions for penalties, records, qualification, shoot-off, status, shooter group, and comments.

Result series payloads contain:

- the phase `TargetPictureHidden`, `SCloudFace`, and `TeamSCloudFace` settings;
- individual or team series;
- per-athlete series, ranks, total score, inner tens, result string, remarks, and team index where applicable.

Exercise result data contains:

- athlete running ID and cloud identifier;
- rank, firing point, exercise ID/name, time zone;
- total string;
- `FaceSelector = "LiveCompetitionResultsView"`;
- shot groups and target picture IDs;
- series total display strings;
- user session information;
- shot coordinates, timestamps, score text, inner-ten/frame-hit flags, direction arrow, and visibility.

Records payloads contain:

- event name/code, phase, record type;
- old/broken result and new result;
- athlete names/license numbers or team members;
- nation and record date.

Binary payloads are used for:

- header left logo: `HeaderLeft.Png`, document type `HeaderLeft`;
- header right logo: `HeaderRight.Png`, document type `HeaderRight`;
- footer logo: `Footer.Png`, document type `Footer`;
- generated report PDFs such as startlists and ranklists.

When cloud export is enabled, changing/removing championship header/footer images also deletes the corresponding cloud document through `SimpleResultserviceApi.FileDeleteAsync(...)`.

## Document Export Hooks

Generated documents can export themselves to cloud.

For startlists:

- uploads competition meta info;
- uploads competition event meta info;
- uploads phase and, for elimination relays, relay subevent meta info;
- uploads shot group definitions;
- uploads `SubEventStartlistPayload`;
- uploads the generated PDF as binary document type `Startlist`.

For ranklists/results:

- uploads shot groups;
- uploads results, result series, exercise result data, records;
- uploads the generated PDF as document type `Ranklist`.

Documents are skipped unless approved/intermediate. When version control is enabled, only valid preliminary/intermediate/approved statuses are considered.

PDF binary metadata includes:

- shooter group;
- file type `PDF`;
- version;
- status;
- description such as relay/stage text;
- start date/time;
- stage index;
- relay index.

## Automatic Model Hooks

When `SCloudExportEnabled` is true:

- championship metadata and logos are pushed from championship save/update paths;
- entry changes can deliver athlete metadata;
- shoot event changes can deliver competition event metadata;
- phase progress/status transitions can deliver event, phase/relay, and shot-group metadata;
- document export can deliver startlists, results, series, shots, records, and PDFs.

The phase status hook does not export when:

- SCloud export is disabled;
- the phase is not the active phase;
- the phase type is `ShootOff`;
- the phase is pre-event training and the event is not clay/shotgun.

## SCloud Maintenance

`SCloudMaintenance` is a maintenance dialog for already exported cloud data. It uses `SimpleResultserviceApi` with bearer authorization.

It displays:

- competition running ID;
- event running IDs;
- subevent running IDs;
- cloud documents for the selected subevent.

Available actions:

- refresh competition data;
- delete complete competition;
- delete selected event;
- delete selected subevent;
- delete selected document/file;
- reset shooter groups for a selected subevent.

Reset shooter groups:

- finds the local phase whose `SIUScloudRunningId` equals the selected cloud subevent running ID;
- sends a fresh `SubEventMetaInfoPayload` with `SCloudConverter.GetPhaseDto(phase)`.

## APIs And Ports

The code uses several cloud clients:

- `SimpleFedappApi`: competition list/details/logos, events, schedules, starters.
- `SimpleResultserviceApi`: result service maintenance, document listing/deletion, competition/event/subevent listing/deletion.
- `LibraryResultApi`: gRPC-web uploads to `InternalResultService`.

Observed ports and URL handling:

- Result API defaults to `https://shootingsportscloud.com:8594/`.
- If token issuer is a `shootingsportscloud.com` host, the result API is `https://{issuer}:8594/`.
- FedApp import API is derived by replacing `8594` with `8596`.
- `SiusCloud.Common` also knows environment hosts:
  - `Prod`: `shootingsportscloud.com`
  - `Stage`: `stage.shootingsportscloud.com`
  - `Alpha`: `alpha.shootingsportscloud.com`
  - `Localhost`: `localhost`
- Shared helpers also reference port `8081` for range/cloud services and port `8597` for another cloud endpoint, but the SiusRank GUI paths above use `8594`/`8596`.

## Practical Notes

- `SCloudExportEnabled` must be on for live/cloud document export. Loading a database resets it off.
- A valid SCloud token and non-basic license are required before the toolbar export toggle is enabled.
- `CloudName` is the name sent to SIUScloud, not necessarily the local championship `Name`.
- The phase SCloud face settings do not change local PDF layout; they are sent in the cloud result-series payload.
- `TargetPictureHidden` controls cloud target picture visibility through the cloud series payload.
- SIUScloud export includes DOB in athlete/startlist payloads even if local startlist reports hide DOB.
- Manual full export only exports phases that are finished, except it skips non-clay pre-event training phases.
- Elimination phases may export relay subevents separately, using each relay's `SIUScloudRunningId`.
