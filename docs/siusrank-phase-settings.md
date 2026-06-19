# SIUS Rank Phase Settings Tab

This document describes the phase-level `Settings` tab shown in the screenshot. It is the `Sius.Rank.Application.Editors.Phase.SettingsControl` control inside `PhaseEditor`, not the global application settings dialog.

The information below was derived by decompiling these assemblies with `ilspycmd`:

- `SiusRank/SiusRank.exe`: `Sius.Rank.Application.Editors.Phase.SettingsControl`
- `SiusRank/Sius.Rank.dll`: `Sius.Rank.Phase`
- `SiusRank/Sius.Rank.dll`: `Sius.Rank.TemplateConfiguration.ShootEvent.PhaseConfiguration`

## Storage Model

Most controls edit properties on the current `Phase` object. Many of those values can also be supplied as defaults in `ShootEventsTemplate*.xml` through `PhaseConfiguration`.

For example, the first phase in `SiusRank/Resources/Templates/ShootEventsTemplate2026_NM_Pistol.xml` contains the same kind of defaults shown in the screenshot. A saved project may differ from the template, for example the screenshot has `Show Medals for Individuals on Scoreboard` checked while this template default is `false`.

```xml
<PhaseConfiguration>
  <PhaseName>Individual</PhaseName>
  <ShootingRankerType>Issf_2013</ShootingRankerType>
  <ShootingRankerTeamType>IssfStandard</ShootingRankerTeamType>
  <DetailReportType>Legacy</DetailReportType>
  <TeamReportType>Legacy</TeamReportType>
  <SCloudFace>Default</SCloudFace>
  <TargetPictureHidden>false</TargetPictureHidden>
  <TeamSCloudFace>Default</TeamSCloudFace>
  <NumberOfRelais>1</NumberOfRelais>
  <ProduceIndividualResults>true</ProduceIndividualResults>
  <ProduceTeamResults>false</ProduceTeamResults>
  <IndividualScoring>Default</IndividualScoring>
  <TeamScoring>Default</TeamScoring>
  <TargetAssignmentBehaviour>LegacyAssignTargetBehaviour</TargetAssignmentBehaviour>
  <EntryAssignmentTargetBehaviour>LegacyEntryAssignmentBehaviour</EntryAssignmentTargetBehaviour>
  <ShowMedalsForIndividualsOnScoreboard>false</ShowMedalsForIndividualsOnScoreboard>
  <ShowMedalsForTeamsOnScoreboard>false</ShowMedalsForTeamsOnScoreboard>
  <StartlistReportType>ImplicitReport</StartlistReportType>
  <ShootOffEveryOtherRoundInReverseOrder>false</ShootOffEveryOtherRoundInReverseOrder>
  <SeparateShootOffForTeamMembers>false</SeparateShootOffForTeamMembers>
  <UseHitMiss>false</UseHitMiss>
  <UseHitMissCalculation>false</UseHitMissCalculation>
  <HitValue>10.0</HitValue>
</PhaseConfiguration>
```

Some visible controls are not part of `PhaseConfiguration` defaults and are only runtime/project phase state, such as `ProgressStatus`, the manual SIUSData file fields, and ranking-list hide flags.

## Visible Settings

| UI label | Screenshot value | `Phase` property / action | Template XML element | Meaning |
| --- | --- | --- | --- | --- |
| `Phase Name` | `Individual` | `Name` | `PhaseName` | Display name of the phase. If empty, SIUS Rank derives a name from the phase type. |
| `Progress Status` | `ReadyToStart` | `ProgressStatus` | Not in `PhaseConfiguration` | Operational phase state. The control is backed by `ProgressStatusPhase`. |
| `Number of Relays` | `1` | `NumberOfRelais` | `NumberOfRelais` | Number of relays configured for this phase. The code keeps the historic spelling `Relais`. |
| `Reorganise Relays` | Button | `ReorganiseRelays(NumberOfRelais)` | Not a stored value | Rebuilds/reorganizes relay assignments using the current number of relays. |
| `Relay Index Offset` | `0` | `RelayIndexOffset` | Not in `PhaseConfiguration` | Offset applied to relay numbering/index display. |
| `Abbreviation for Startlist Remark` | Empty | `AbbreviationForStartlistRemarks` | `AbbreviationForStartlistRemarks` | Short text used as the start-list remark abbreviation for this phase. |
| `Target picture is hidden` | Off | `TargetPictureHidden` | `TargetPictureHidden` | Hides target pictures for this phase. |
| `Individual SCloud Face` | `Default` | `SCloudFace` | `SCloudFace` | Selects the SIUScloud face/layout for individual results. Values come from `SCloudFaceType`. |
| `Team SCloud Face` | `Default` | `TeamSCloudFace` | `TeamSCloudFace` | Selects the SIUScloud face/layout for team results. Values come from `TeamSCloudFaceType`. |
| `Produce Individual Results` | On | `ProduceIndividualResults` | `ProduceIndividualResults` | Enables individual result production for the phase. |
| `Individual Report` | `Legacy` | `ReportType` | `DetailReportType` | Selects the individual result report implementation. Values come from `DetailReportType`. |
| `Individual Scoring` | `Default` | `IndividualScoring` | `IndividualScoring` | Selects the individual scoring algorithm. Values come from `IndividualScoring`. |
| `Shooting Ranker` | `Issf_2013` | `ShootingRankerType` | `ShootingRankerType` | Selects the individual ranking algorithm. Deprecated enum values are filtered out in the UI. |
| `Show Medals for Individuals on Scoreboard` | On | `ShowMedalsForIndividualsOnScoreboard` | `ShowMedalsForIndividualsOnScoreboard` | Shows medal marks for individual entries on scoreboards. |
| `Qualification Remark Editor Individual` | Button | `QualificationRemarkDefinitionsIndividuals` | `QualficationRemarkDefinitionsIndividuals` | Opens an editor for individual qualification remark definitions. The template property is misspelled as `Qualfication...` in code. |
| `Produce Team Results` | Off | `ProduceTeamResults` | `ProduceTeamResults` | Enables team result production for the phase. |
| `Team Report` | `Legacy` | `TeamReportType` | `TeamReportType` | Selects the team result report implementation. Values come from `TeamDetailReportType`. |
| `Team Scoring` | `Default` | `TeamScoring` | `TeamScoring` | Selects the team scoring algorithm. Values come from `TeamScoring`. |
| `Shooting Ranker Team` | `IssfStandard` | `ShootingRankerTeamType` | `ShootingRankerTeamType` | Selects the team ranking algorithm. |
| `Show Medals for Teams on Scoreboard` | Off | `ShowMedalsForTeamsOnScoreboard` | `ShowMedalsForTeamsOnScoreboard` | Shows medal marks for team entries on scoreboards. |
| `Shoot Off every other round in reverse order` | Off | `ShootOffEveryOtherRoundInReverseOrder` | `ShootOffEveryOtherRoundInReverseOrder` | Changes team shoot-off ordering so alternating rounds are shot in reverse order. |
| `Separate shoot off for team members` | Off | `SeparateShootOffForTeamMembers` | `SeparateShootOffForTeamMembers` | Runs separate shoot-offs for members of a team. |
| `Qualification Remark Editor Team` | Button | `QualificationRemarkDefinitionsTeams` | `QualficationRemarkDefinitionsTeams` | Opens an editor for team qualification remark definitions. The template property is misspelled as `Qualfication...` in code. |
| `Number of Points to Win` | `0`, disabled | `NumberOfPointsToWin` | `NumberOfPointsToWin` | Point target for point-based finals or point-based scoring modes. |
| `Win Points` | `0`, disabled | `WinningPoints` | `WinningPoints` | Points awarded for winning a contest/series when point scoring is active. |
| `Tie Points` | `0`, disabled | `TiePoints` | `TiePoints` | Points awarded for a tie when point scoring is active. |
| `Loose Points` | `0`, disabled | `LosingPoints` | `LosingPoints` | Points awarded for losing when point scoring is active. The UI spells this `Loose Points`. |
| `Point Difference to Win` | `0`, disabled | `PointDiffToWin` | `PointDifferenceToWin` | Minimum point difference needed to win in supported point-based modes. |
| `Hide Startnumber on Ranking List` | Off, enabled | `HideStartNumberOnRanklists` | Not in `PhaseConfiguration` | Suppresses start numbers on generated ranking lists when enabled. |
| `Hide Targetname on Ranking List` | Off, enabled | `HideTargetNameOnRanklists` | Not in `PhaseConfiguration` | Suppresses target names on generated ranking lists when enabled. |
| `Manual Target Mapping Mode` | Off | `ManualTargetMappingModeEnabled` | Not in `PhaseConfiguration` | Enables manual target mapping for this phase. |
| `Edit` under target mapping | Disabled | Opens `TargetMappingEditor` | `ManualTargetMappingModePreferredTemplate` stores only preferred template name | Edits manual target mappings. Enabled only when manual target mapping mode is checked. |
| `Use Hit/Miss mode` | Off | `UseHitMiss` | `UseHitMiss` | Switches the phase to hit/miss mode. |
| `Calculate Hit/Miss from Score` | Off, disabled | `UseHitMissCalculation` | `UseHitMissCalculation` | Converts incoming score values into hit/miss values instead of expecting hit/miss data directly. |
| `Scorelevel for Hit` | `10`, disabled | `HitValue` | `HitValue` | Score threshold/value for converting a score into a hit. Enabled only when hit/miss mode and calculation from score are both enabled. |
| `Target for Hit/Miss` | `HitMiss Target`, disabled | `HitMissTarget` | `HitMissTarget` | Selects the hit/miss target model. Values are supplied by `HitMissTargetFactory`. |
| `SIUSData File` | Off | `ManualCsvFileNameEnable` | Not in `PhaseConfiguration` | Enables a manual SIUSData CSV filename override for this phase. |
| Filename under `SIUSData File` | `Fri_commonI.csv`, disabled | `ManualCsvFileName` | Not in `PhaseConfiguration` | Manual CSV filename/path. The text box is enabled only when `SIUSData File` is checked. Validation requires a `.csv` extension. |
| `Starters Transfer Method` | `Legacy` | `EntryAssignmentTransferBehaviourType` | `EntryAssignmentTargetBehaviour` | Selects how starters/entry assignments are transferred into this phase. Values come from `EntryAssignmentBehaviourFactory`. |

## Enablement Rules

The screenshot shows several disabled controls. The decompiled `SettingsControl.UpdateControls()` and `SetVisibilityOfHitMissControls()` methods explain the rules:

- `SIUSData File` can only be toggled for elimination, qualification, individual, shoot-off, KO, and final phases.
- The SIUSData filename text box is enabled only when `ManualCsvFileNameEnable` is checked.
- `Manual Target Mapping Mode > Edit` is enabled only when `ManualTargetMappingModeEnabled` is checked.
- The point-scoring group is enabled only for point-based finals or point-based individual/team scoring modes:
  - KO phases using `CupByPoints`, `CupByPoints2`, `SportPistol`, `MixedTeamEvent`, or `TopTeam`
  - individual scoring `PointsBasedOnParticipantsInCompetition`, `PointsBasedOnSeriesScore`, or `PointsBasedOnTotalScore`
  - team scoring `SeriesScorePerTeam`, `SeriesScorePerTeamMember`, `WinningPointPerTeamBasedOnIndividualContestResults`, or `PointsBasedOnParticipantsInCompetition`
- `Hide Startnumber on Ranking List` and `Hide Targetname on Ranking List` are enabled only when individual results use `Legacy` reports or team results use `Legacy` reports.
- Hit/miss dependent controls are disabled until `UseHitMiss` is enabled.
- `Scorelevel for Hit` is enabled only when both `UseHitMiss` and `UseHitMissCalculation` are enabled.

## Values Populated By Factories Or Enums

The UI does not hard-code every combo-box option. It fills the controls as follows:

| Control | Source |
| --- | --- |
| `Progress Status` | `ProgressStatusPhase` enum |
| `Shooting Ranker` | `ShootingRankerType` enum, excluding values whose name contains `deprecated` |
| `Shooting Ranker Team` | `ShootingRankerTeamType` enum |
| `Individual Scoring` | `IndividualScoring` enum |
| `Team Scoring` | `TeamScoring` enum |
| `Individual Report` | `DetailReportType` enum |
| `Team Report` | `TeamDetailReportType` enum |
| `Individual SCloud Face` | `SCloudFaceType` enum |
| `Team SCloud Face` | `TeamSCloudFaceType` enum |
| `Starters Transfer Method` | `EntryAssignmentBehaviourFactory.Instance.GetViewModels()` |
| `Target for Hit/Miss` | `HitMissTargetFactory.Instance.GetViewModels()` |

## Fields Visible Below The Screenshot Crop

The decompiled control also includes these Settings-tab fields just below/near the cropped bottom of the screenshot:

| UI label | `Phase` property | Template XML element |
| --- | --- | --- |
| `Target Assign Method` | `TargetAssignmentBehaviourType` | `TargetAssignmentBehaviour` |
| `Startlist report type` | `StartlistReportType` | `StartlistReportType` |
| scoring value property selector | `DefaultScoringvalueProperty` | Not in `PhaseConfiguration` |
