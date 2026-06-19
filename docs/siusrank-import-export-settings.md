# SIUS Rank Import / Export Settings

This document describes the global `General Settings > Import / Export` page in SIUS Rank. This is the dialog shown from the main settings menu, not the phase-level `Import / Export` page used for starter CSV imports such as `Update starters from file`.

The page contains two config groups:

- `ShotImport`: how SIUS Rank receives live shot data.
- `DataExport`: how SIUS Rank publishes reports, live ODF, MQTT, STYX scoreboard data, and RTS/Omega data.

## Storage

These settings are global application settings, not championship database fields. They are saved through SIUS Rank's `ConfigService` as XML in `Settings.xml`.

On a normal Windows installation the path is usually:

```text
C:\ProgramData\SIUS AG\SiusRank\Settings\Settings.xml
```

If that directory does not already exist, SIUS Rank falls back to the installation directory:

```text
<SiusRank install directory>\Settings\Settings.xml
```

The XML model is a simple `Settings` document with one element per config group and one child per parameter:

```xml
<Settings>
  <ShotImport>
    <ImportMode value="TCPIP" />
    <SiusDataDataPath value="C:\Program Files\Sius Ascor\SiusData\Data" />
    <ImportInterval value="60" />
    <SiusDataIPAddress value="127.0.0.1" />
    <SiusDataIpPort value="4000" />
  </ShotImport>
  <DataExport>
    <OdfServerPort value="4100" />
    <ReportDefaultFont value="Arial, 8pt" />
    <MqttBrokerAddress value="127.0.0.1" />
    <StartInternalMqttBroker value="True" />
    <StyxScoreboardSource value="False" />
    <PublishOmegaFormat value="False" />
    <SaveOmegaFormat value="False" />
    <UseStyxCountdown value="False" />
  </DataExport>
</Settings>
```

## Shot Import Configuration

The shot import settings control `ShotImporterService`. The service selects one importer based on `ImportMode`, converts external data to `ShotDescriptor[]`, and sends the shots to the active phase. The active phase then maps each `ShotDescriptor.StartNumber` to an `EntryAssignment.StartNumber` and stores the attached `IShot`.

| UI label | Config field | Default | Meaning |
| --- | --- | --- | --- |
| `TCP/IP` / `csv-File` / `MQTT` / `STYX` | `ImportMode` | `TCPIP` | Selects the live shot import backend. |
| `SiusData Host IP` | `SiusDataIPAddress` | `127.0.0.1` | TCP/IP host used only by `TCP/IP` mode. |
| Port next to host IP | `SiusDataIpPort` | `4000` | TCP/IP port used only by `TCP/IP` mode. |
| Data path, visible in `csv-File` mode | `SiusDataDataPath` | `C:\Program Files\Sius Ascor\SiusData\Data` | Folder where SIUS Rank looks for SIUS Data CSV shot files. |
| `Ranking-Interval` | `ImportInterval` | `60` seconds | Minimum accepted value is 10 seconds. Used differently by each importer. |

### Import Mode: TCP/IP

`TCP/IP` connects to the SIUS Data feed at `SiusDataIPAddress:SiusDataIpPort`. With the screenshot defaults, SIUS Rank connects to:

```text
127.0.0.1:4000
```

This mode receives SIUS Data messages such as `_SHOT`, `_ROUND`, `_GRPH`, `_PRCH`, `_CLRS`, `_DIAG`, and `_SHOOTER_SELECTED`.

For `_SHOT`, SIUS Rank parses the semicolon-separated packet into an `IShot`:

- `TargetNumber`
- `StartNumber`
- shot timestamp
- log type
- score values
- inner-ten, miss, frame-hit, sighter, overtime, demo, ignored-shot flags
- X/Y coordinates
- target code

For `_ROUND`, SIUS Rank creates one hit/miss style shot descriptor per character in the round shot pattern. This is mainly used by shotgun or round-result style messages.

`Ranking-Interval` is not a polling interval in this mode. TCP data is received continuously. The interval controls how often SIUS Rank requests ranking after new shots have arrived.

### Import Mode: csv-File

`csv-File` watches a SIUS Data CSV file. The folder comes from `SiusDataDataPath`. The filename is normally generated from the active phase:

```text
<ShootEvent.ShortName><first letter of PhaseType>.csv
```

For example, a qualification phase may produce a filename like:

```text
AP60Q.csv
```

A phase can override this with its manual CSV filename setting.

The importer reads the file as ASCII on every `Ranking-Interval`, remembers the highest imported line number, and imports only new lines on later cycles. Changing the watched filename resets that line counter.

Expected SIUS Data CSV columns are:

```text
StartNumber,Score,Phase,TargetNumber,Score2,Score3,Time,IsInnerTen,CoordinateX,CoordinateY,IsInTime,LightPhaseTimeSpan,IsRightSweep,IsDemo,ShootOrdinal,PracticeOrdinal,ManualStatus,TotalKind,GroupOrdinal,FireKind,LogEventId,LogType,Date,Relay,Weapon,Position,TargetCode,ExternalNumber
```

Important parsing rules:

- `StartNumber` is the athlete matching key.
- `TargetNumber` is copied to the shot, but the active phase still assigns the shot by start number.
- `Date` is a SIUS hundreds-of-seconds value and is combined with the current year.
- `IsInTime = 0` marks overtime.
- `ManualStatus = 1` imports as a manual/demo score.
- `ManualStatus = 2` imports as ignored/demo.
- `LogType = 2` is skipped.
- `LogType = 10` adds a `CrossShot` comment.
- `LogType = 12` adds an `IllegalShot` comment.
- `TargetCode` and `ExternalNumber` are optional trailing fields.

### Import Mode: MQTT

`MQTT` uses SIUS Rank's MQTT client service. The importer itself only subscribes to events from the client; it does not start the client connection. In practice, the main toolbar `MQTT` export toggle must be enabled, or some other code must already have started the MQTT client.

The MQTT client connects to:

```text
<MqttBrokerAddress>:1883
```

The port is fixed at `1883`; the settings dialog only exposes the broker address.

Shot import subscribes to:

- `siusrank/roundresult`: parsed as a SIUS Data `_ROUND` packet.
- `practicecontrol/shot`: parsed as `RunningTargetShotExportMessage`.

`practicecontrol/shot` messages create shots with:

- `StartNumber` from the message's `StartNumber`
- lane as `TargetNumber`
- shot id, shot number, timestamp
- first and second score values
- inner-ten, miss, frame-hit, demo and sighter flags
- running-target movement direction
- X/Y coordinates in millimeters

In this version, `Ranking-Interval` is not used by the MQTT importer.

### Import Mode: STYX

`STYX` reads result data from the STYX/Kanopus resource stack instead of SIUS Data files or SIUS Data TCP packets.

When started, the importer:

1. Looks at the active phase entry assignments.
2. Collects non-empty `StyxAssignmentId` and `StyxAssignmentIdShootOff` values.
3. Connects an `ActiveConnectionContainerExerciseInitializedFromDB` with `ConnectToResultData = true`.
4. Listens for `ExerciseResultData` changes for those correlation ids.
5. Converts new Artemis shots to SIUS Rank `ShotDescriptor` objects.

The STYX result data must contain:

- `UserSessionInformation.UserData.MemberId`: parsed as the SIUS Rank start number.
- `FiringPoint`: used as firing point/lane information.
- shots returned by `GetAllShots()`: converted to SIUS Rank shot fields.

`Ranking-Interval` starts a timer that periodically requests ranking while the STYX importer is running.

## Shot Import Data Model

All four import modes eventually produce the same internal model:

```csharp
public struct ShotDescriptor
{
    public int StartNumber { get; set; }
    public IShot Shot { get; set; }
    public TimeSpan? ShotTimeOffset { get; set; }
    public bool ManualScore { get; set; }
    public bool IgnoreHitMissHack { get; set; }
    public bool IgnoreShotImportOverwrite { get; set; }
}
```

The active phase imports those descriptors like this:

1. Discard descriptors where `Shot.ExternalNumber` does not match the active phase external number.
2. If `Shot.TargetCode` is `65535`, replace it with the shoot event target code.
3. Map `ShotDescriptor.StartNumber` to an entry assignment in the active phase.
4. Apply selected-shooter or import-filter override mode if enabled in the UI.
5. Set shoot-off status and hit/miss conversion when relevant.
6. Add or update shots on the matching `EntryAssignment`.
7. Trigger phase change events and, depending on phase ranking behavior, ranking.

The key point is that start number is the normal join key. Target number is shot metadata and is not the primary matching key for imported shots.

## Data Export Settings

The data export settings control report font, ODF server setup, MQTT broker/client behavior, STYX scoreboard source behavior, and RTS/Omega publishing.

| UI label | Config field | Default | Meaning |
| --- | --- | --- | --- |
| `Report Default Font` | `ReportDefaultFont` | `Arial, 8pt, Regular` | Default report font used by DevExpress report generation. |
| `Odf-Server Port` | `OdfServerPort` | `4100` | TCP port used when the toolbar ODF export toggle starts the ODF server. |
| `MQTT Broker Address` | `MqttBrokerAddress` | `127.0.0.1` | MQTT host used by SIUS Rank's MQTT client. Port is fixed to `1883`. |
| `Start Internal MQTT Broker` | `StartInternalMqttBroker` | `true` | Starts an embedded MQTT broker in SIUS Rank. |
| `Exercise data provider for STYX Scoreboards` | `StyxScoreboardSource` | `false` | Makes SIUS Rank announce itself as the exercise view data source for STYX/range scoreboards. |
| `Use STYX Countdown` | `UseStyxCountdown` | `false` | Uses the STYX/range timer service instead of SIUS Rank's local timer controller. |
| `Publish Omega Data` | `PublishOmegaFormat` | `false` | Publishes RTS/Omega interface messages over MQTT. |
| `Save Omega Data` | `SaveOmegaFormat` | `false` | Writes RTS/Omega interface JSON files to the export folder. |

### Report Default Font

This is used by SIUS Rank reports through `FontConfigurator`. It changes generated report fonts, especially the font family used by labels, table rows and table cells. Report layout still comes from the report definitions, so changing the font can affect fit and wrapping.

### ODF Server Port

The ODF server is controlled by the main toolbar ODF export toggle, not directly by this settings checkbox page.

When ODF export is enabled, SIUS Rank:

1. Reads `OdfServerPort`.
2. Checks whether the port is available.
3. Starts `OdfOvrExporter.ConnectionServer`.
4. Broadcasts ODF XML messages to connected TCP clients.

Changing the port while the ODF server is already running does not restart the server. Toggle ODF export off and on after changing the port.

ODF messages are also sent through MQTT as compressed payloads on:

```text
siusrank/compressedodfmessage
```

That MQTT path only delivers if the MQTT client is connected.

### MQTT Broker Address

`MqttBrokerAddress` is the host used by SIUS Rank's MQTT client. The client always uses TCP port `1883`.

The MQTT client publishes and subscribes to several SIUS Rank and STYX/range-scoreboard topics. Examples:

```text
siusrank/compressedodfmessage
siusrank/csv/<relative-folder>/startlist
siusrank/csv/<relative-folder>/ranklistindividual
siusrank/csv/<relative-folder>/ranklistteam
siusrank/csv/<relative-folder>/liveshot
siusrank/rtsomegainterface
rangescoreboard/siusrank/startlist/<group-or-id>
rangescoreboard/siusrank/ranklist/<group-or-id>
rangescoreboard/siusrank/ranklistteam/<group-or-id>
rangescoreboard/configuration/tablemapping/
siusrank/exerciseviewdata/lane_001
```

The main toolbar MQTT export toggle starts and stops this client. Without an active MQTT client connection, MQTT publishing settings have no practical effect.

### Start Internal MQTT Broker

When enabled, SIUS Rank starts an embedded MQTTnet broker at application startup. The broker uses MQTTnet's default endpoint, effectively port `1883`.

This setting does not change `MqttBrokerAddress`. Common setups are:

- Local all-in-one machine: start internal broker and keep `MqttBrokerAddress = 127.0.0.1`.
- External broker: disable internal broker and set `MqttBrokerAddress` to the external broker host.

If another broker is already using port `1883`, the internal broker can fail to start.

### Exercise Data Provider For STYX Scoreboards

When enabled and the MQTT client connects, SIUS Rank publishes a retained producer-selection message to:

```text
range/lanespectatorviewsource
```

The payload selects `SiusRank` as the current exercise view data producer. When the MQTT client stops, SIUS Rank switches the retained producer back to `ExerciseController`.

This setting is for STYX/range scoreboards that should display SIUS Rank's calculated view of the exercise. SIUS Rank can then publish:

- exercise view data per lane on `siusrank/exerciseviewdata/lane_###`
- target-table mappings on `rangescoreboard/configuration/tablemapping/`
- range scoreboard start lists, rank lists, headers, logos and presentation data

### Use STYX Countdown

When disabled, SIUS Rank creates its own local timer controller on port `35645` and broadcasts local timer values.

When enabled, SIUS Rank uses the injected STYX/range timer service (`IRangeTimer`) and follows the timer state from that service. This affects the SIUS Rank timer bar and countdown data used by connected scoreboard flows.

This setting is independent of the selected shot import mode. You can import shots by TCP/IP and still use STYX countdown, or import via STYX and use the local timer, although mixed setups should be tested before match day.

### Publish Omega Data

When enabled, SIUS Rank creates RTS/Omega interface messages and publishes them over MQTT to:

```text
siusrank/rtsomegainterface
```

Published message types include:

- full rankings from start lists and rank lists
- team rankings where the phase produces team results
- partial updates for non-sighter live shots
- current-athlete changes

This requires the MQTT client to be connected.

### Save Omega Data

When enabled, the same RTS/Omega interface model is written to disk under the active phase export folder.

Generated filenames look like:

```text
yyyy-MM-dd-HHmmss-<message-id>-fullranking.json
yyyy-MM-dd-HHmmss-<message-id>-partialUpdate.json
```

Each JSON file contains:

- `ExchangeHeader`
- `FullRanking` or `PartialUpdate`

Saving does not require MQTT connectivity.

## Export Data Model

Export is driven by generated SIUS Rank documents and live shot events:

```text
Rank list / start list / live shot
        |
        v
ExportService / OdfOvrExporter / CsvExportViaMqtt / RtsOmegaInterfaceExporter
        |
        +-- CSV files under <database folder>\Exports
        +-- .stl.csv SIUS Data start-list files
        +-- ODF XML over TCP, and compressed ODF over MQTT
        +-- STYX/range-scoreboard MQTT payloads
        +-- RTS/Omega MQTT payloads and optional JSON files
```

Important persistent and runtime objects:

- `Championship`: owns export paths and non-persistent toolbar toggles such as `OdfExportEnabled`, `MqttExportEnabled`, `SCloudExportEnabled`, and `FtpTransferEnabled`.
- `Phase`: owns active entry assignments, relays, target mappings, STYX assignment ids, and shot importer hookup.
- `EntryAssignment`: links an athlete/start number to a phase, relay, target, team, STYX ids and imported shots.
- `Shot`: stores score values, target number, timestamp, target code, coordinates, sighter/overtime/miss/frame-hit flags and live export state.
- `DocumentWithEntries`: base model for generated start lists, rank lists and team rank lists.

The global `DataExport` settings define transport and format behavior. The championship toolbar toggles decide whether ODF and MQTT export are currently running.

## Practical Recommendations

For a local SIUS Data setup on the same PC:

- Use `TCP/IP`.
- Keep `SiusData Host IP = 127.0.0.1`.
- Keep port `4000` unless SIUS Data is configured differently.
- Use a `Ranking-Interval` of at least `10`; `15` is a reasonable live-ranking value.

For local MQTT scoreboards:

- Enable `Start Internal MQTT Broker`.
- Keep `MQTT Broker Address = 127.0.0.1`.
- Enable the toolbar `MQTT` export toggle before expecting MQTT imports or MQTT exports.

For an external MQTT broker:

- Disable `Start Internal MQTT Broker`.
- Set `MQTT Broker Address` to the external broker host.
- Ensure TCP port `1883` is reachable.

For STYX/range scoreboards:

- Enable `Exercise data provider for STYX Scoreboards` only when the scoreboard clients should use SIUS Rank as their exercise view data source.
- Enable `Use STYX Countdown` only when the STYX/range timer service is the authoritative competition timer.
- Verify active phase entries have valid STYX assignment ids before relying on `STYX` shot import mode.

For RTS/Omega integration:

- Use `Save Omega Data` first to inspect generated payloads without depending on MQTT.
- Enable `Publish Omega Data` only after the MQTT broker and consumer have been verified.
