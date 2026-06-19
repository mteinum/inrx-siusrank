# NM2026 startlagregler

Dette dokumentet beskriver praktiske regler for å legge skyttere inn i startlag for NM Bane Pistol 2026. Reglene brukes ved manuelle endringer i `storage.db3`, CSV-eksport og SIUS Rank-synk.

## Prioritet

Når regler kolliderer, bruk denne prioriteten:

1. Kapasitet og gyldig skiveoppsett.
2. Våpendeling og andre eksplisitte skytterkonflikter.
3. Samling av små klasser og SH1.
4. Seeding etter reglement 2.9.5.
5. Finale- og tidskonflikter.
6. Klassevis orden inne i startlag.
7. Klubbspredning og øvrig rydding.

Ikke bryt en hard regel for å forbedre en preferanseregel.

## Kapasitet

Maks kapasitet per startlag:

| Øvelse | Kapasitet |
| --- | ---: |
| Fripistol | 20 |
| Silhuett | 14 |
| Standard | 35 |
| Finpistol | 35 |
| Grovpistol | 35 |
| Hurtig Fin | 35 |
| Hurtig Grov | 35 |

Et startlag skal aldri ha flere skyttere enn kapasiteten. Etter flytting skal det ikke finnes dupliserte skiver innen samme øvelse og startlag.

## Klasseblokker

Skyttere i samme klasse skal ligge samlet i startlaget så langt det er mulig.

Små klasser skal alltid samles i ett startlag:

- `Jk`
- `Jm`
- `Jr-NM`
- `U-NM`
- `V65`
- `V73`
- `SH1-P3`
- `SH1-P4`

Hvis en liten klasse ikke får plass i ett startlag, skal det behandles som en hard avvikssak før publisering.

Innen hvert startlag sorteres skyttere etter InrX sin `Mklasse.sort`, deretter skive. Dagens praktiske rekkefølge er:

```text
U-NM, Jk, Jm, Jr-NM, K, M, Å, V55, V65, V73, SH1
```

For Fripistol og andre øvelser der klasseblokker er fordelt på flere startlag, kan samme klasse finnes i flere startlag når kapasitet eller andre harde regler krever det. Unngå enkeltstående skyttere i en klasse hvis de kan flyttes til et startlag med samme klasse uten å bryte andre regler.

## Seeding

Reglement 2.9.5 gjelder når det er flere deltakere i senior- og juniorklassene enn banen har kapasitet til i ett startlag.

Praktiske regler:

- Inntil 15 antatt beste skyttere i hver aktuell klasse skal seedes i samme startlag.
- Seedingen baseres på NSF ranking, NM 2025 og approberte resultater fra relevant periode.
- Seedet lag skal normalt være siste startlag i klassen.
- Hold klassen som sammenhengende blokk også når seedgruppen ikke fyller hele startlaget.
- Ikke flytt seedede skyttere ut av seedet lag uten eksplisitt beslutning.
- Hvis mulig, hold 1-2 plasser åpne for etteranmeldte i seedet lag.

Seeding skal ikke bryte kapasitet, våpendeling eller eksplisitte tidskonflikter.

## Finaler og tidskonflikter

Skyttere som forventes å skyte finale i Finpistol eller Silhuett skal ikke ligge i siste kvalifiseringslag for den øvelsen.

Dette gjelder generelt for klasser med finale. Følgende har ikke finale:

- Finpistol: `Jm`, `M`, `U-NM`, `V55`, `V65`, `V73`
- Hurtig Fin: ingen finale
- Hurtig Grov: ingen finale

Finpistol-startlag skal ligge før Grovpistol-startlag. Skyttere som skyter både Finpistol og Grovpistol skal ikke ligge i siste Finpistol-lag og første Grovpistol-lag.

## Våpendeling

Skyttere som deler våpen skal ikke ligge i samme startlag i de aktuelle øvelsene.

Registrerte våpendelinger:

| Skyttere | Øvelse(r) |
| --- | --- |
| Stein Myrvang / Truls Myrvang | Grovpistol, Hurtig Grov |
| Kine-Merethe Pettersen / Kai Rosenlund | Fripistol |
| Irene Askeland / Ole-Harald Aas | Fripistol |

Begge skyttere i en våpendeling skal ha InrX-kommentar på den aktuelle `Resultat`-raden:

```text
Våpendeling: deler pistol med <navn> - ikke samme startlag.
```

Etter flytting skal våpendeling valideres eksplisitt. Hvis en flytting ville satt et våpendelingspar i samme startlag, må enten en annen skytter flyttes, eller hele klasseblokken flyttes hvis det bevarer bedre klasseorden.

## Silhuett / RFP

Silhuett kvalifisering bruker to skyttere per skivestativ. I InrX representeres dette med side-skiver:

```text
2, 4, 7, 9, 12, 14, 17, 19, 22, 24, 27, 29, 32, 34
```

Mapping:

| Side-skive | Importfilter | SIUS Data Start Number |
| ---: | --- | ---: |
| 2 | V | 1000 |
| 4 | H | 1000 |
| 7 | V | 2000 |
| 9 | H | 2000 |
| 12 | V | 3000 |
| 14 | H | 3000 |
| 17 | V | 4000 |
| 19 | H | 4000 |
| 22 | V | 5000 |
| 24 | H | 5000 |
| 27 | V | 6000 |
| 29 | H | 6000 |
| 32 | V | 7000 |
| 34 | H | 7000 |

SIUS Rank skal ha `TwoShootersPerLane = 1` for Silhuett kvalifisering. `ImportShotFilter` og `SiusDataStartNumber` håndteres i `.srkl`-synken, ikke ved å endre CSV-formatet.

Finalefaser og resultatdata skal ikke endres ved startlagsarbeid.

## Klubbspredning

Unngå skyttere fra samme klubb ved siden av hverandre når det er praktisk mulig.

Dette er en preferanse, ikke en hard regel. Ikke bryt kapasitet, klasseblokker, seeding, våpendeling eller finalehensyn for å spre klubber.

Hvis klubbspredning ikke lar seg løse uten å bryte viktigere regler, behold startlisten og rapporter avviket som `Vurderes`.

## Praktisk endringsflyt

Ved endring av startlag:

1. Ta backup av `Stevner/NM2026/storage.db3`.
2. Ta backup av `Stevner/NM2026/NM Bane Pistol 2026.srkl` hvis SIUS Rank skal synkes.
3. Endre `Resultat.startLagId`, `Resultat.standplass`, `Resultat.skivenrFra` og `Resultat.skivenrTil` i InrX-data.
4. Behold `StartLag.dato` med mindre selve tidsplanen skal endres.
5. Sorter berørte startlag etter klasse og skive.
6. Regenerer CSV-eksport.
7. Synk SIUS Rank assignments fra InrX.
8. Regenerer HTML- og PDF-rapporter.
9. Kjør validering før filene brukes videre.

## Validering

Før en endring er ferdig, skal dette være kontrollert:

- Totalt antall InrX-startere er uendret hvis det bare er startlagsflytting.
- CSV-antall matcher InrX-antall.
- SIUS Rank aktive assignments matcher InrX.
- Ingen ekstra eller manglende SIUS assignments.
- Ingen dupliserte skiver i samme øvelse/startlag.
- Ingen startlag over kapasitet.
- Små klasser og SH1 er samlet.
- Våpendelingspar ligger ikke i samme startlag.
- Silhuett har korrekt V/H og `SiusDataStartNumber`.
- Berørte rapporter og PDF-er er regenerert.

Hvis noen av disse punktene feiler, skal endringen ikke publiseres før avviket er løst eller eksplisitt akseptert.
