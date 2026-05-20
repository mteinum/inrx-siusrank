# teinum-inrx-siusrank

CLI-verktøy for å lage SIUS Rank starter-importfiler fra en inrX `storage.db3` SQLite-database.

Programmet leser påmeldinger/startdata fra inrX og skriver CSV-filer som kan brukes i SIUS Rank via:

- `Update starters from file`
- `Update starters from clipboard`

Verktøyet lager ikke en egen SIUS Data-startliste. SIUS Rank lager SIUS Data-startlisten videre fra importerte startere.

## Hva eksporteres

CSV-en følger SIUS Rank sitt starter-importformat, samme type header som `SiusRank_importExample.csv`:

```text
StartNumber;AccreditationNumber;IssfId;DisplayNameLong;DisplayName;FirstName;Name;BirthDay;Gender;Nation;BibNumber;TargetNumber;Relay;TeamIndex;DuellIndex;Groups;Comment;StarterId;TeamPosition;Team;TeamDisplay;TeamDuellIndex;TeamComment
```

Viktige mappinger:

- `StartNumber`, `AccreditationNumber`, `BibNumber` og `StarterId` settes til inrX `Resultat.Id`.
- KM/NM-klasse hentes fra `Resultat.MklasseId1`.
- `Groups` settes fra KM/NM-klasse, for eksempel `Å -> Apen`, `V55 -> V55`, `Jm -> Jrm`.
- `Team` og `TeamDisplay` kan fylles med klubbkortnavn med `--include-club-team`.
- Navn beholdes i full lengde slik de vises i SIUS Rank.

## Kjør interaktivt

Fra repo-roten:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- --wizard --db storage.db3
```

Wizard lar deg velge stevne, øvelse, KM/NM-klasse og om data skal til fil, clipboard eller begge deler.

## Lag én importfil

Eksempel for Fripistol, KM/NM klasse `Å`:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --ovelse Fripistol \
  --klasse Å \
  --output NM50FRI_APEN_import.csv \
  --include-club-team
```

Til clipboard i stedet for fil:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --ovelse Fripistol \
  --klasse Å \
  --clipboard \
  --include-club-team
```

## Lag importfiler for alle klasser

Én fil per KM/NM-klasse for ett stevne:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-id 405 \
  --all-classes \
  --output-dir siusrank-import \
  --include-club-team
```

Én fil per KM/NM-klasse for flere stevner:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --all-classes \
  --output-dir siusrank-import \
  --include-club-team
```

`--stevne-ids` støtter både lister og intervaller:

```text
405,406,407
405-411
405-407,409,411
```

Eksempel på filer som blir laget:

```text
20260706_Fri_Apen.csv
20260706_Fri_V55.csv
20260706_Fri_V65.csv
20260706_Fri_V73.csv
20260706_Fri_SH1-P4.csv
```

## Valider shooter groups

Hvis du har SIUS Rank sin `ShooterGroupsTemplate.xml`, kan du validere at `Groups`-verdiene i eksporten finnes i SIUS Rank-oppsettet:

```bash
dotnet run --project InrxToSiusRank/src/InrxToSiusRank -- \
  --db storage.db3 \
  --stevne-ids 405-411 \
  --all-classes \
  --output-dir siusrank-import \
  --include-club-team \
  --shooter-groups-template path/to/ShooterGroupsTemplate.xml
```

Dette endrer ikke eksporten. Det stopper bare kjøringen hvis en `Groups`-verdi ikke finnes i templatefilen.

## Bygg Windows exe

Lag en selvstendig Windows-kjørbar fil:

```bash
dotnet publish InrxToSiusRank/src/InrxToSiusRank/InrxToSiusRank.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true
```

Exe-filen havner her:

```text
InrxToSiusRank/src/InrxToSiusRank/bin/Release/net8.0/win-x64/publish/InrxToSiusRank.exe
```

På Windows:

```powershell
.\InrxToSiusRank.exe --wizard --db .\storage.db3
```

Bulk-eksport på Windows:

```powershell
.\InrxToSiusRank.exe --db .\storage.db3 --stevne-ids 405-411 --all-classes --output-dir .\siusrank-import --include-club-team
```

## Viktige valg

```text
--db <path>                         Path to storage.db3.
--wizard                            Start interaktiv wizard.
--stevne-id <id>                    Velg ett Stevne.Id.
--stevne-ids <ids>                  Velg flere stevner, f.eks. 405,406 eller 405-411.
--event-date <yyyy-MM-dd>           Velg stevne etter dato.
--event-name <text>                 Filtrer stevne etter navn sammen med --event-date.
--ovelse <name>                     Velg øvelse, f.eks. Fripistol.
--ovelse-id <id>                    Velg OvelseDef.Id.
--klasse <value>                    Velg KM/NM-klasse, f.eks. Å, V55, V65.
--km-nm-klasse <value>              Samme som --klasse.
--all-classes                       Lag én fil per KM/NM-klasse.
--output <path>                     Fil for vanlig eksport.
--output-dir <path>                 Mappe for --all-classes.
--clipboard                         Kopier importdata til clipboard.
--copy-to-clipboard                 Samme som --clipboard.
--include-club-team                 Fyll Team og TeamDisplay med klubbkortnavn.
--sius-group <value>                Overstyr Groups for vanlig eksport.
--shooter-groups-template <path>    Valider Groups mot SIUS Rank-template.
--encoding <utf8-bom|windows-1252>  Encoding. Default: utf8-bom.
```

## Test

```bash
dotnet test InrxToSiusRank/InrxToSiusRank.sln
```

