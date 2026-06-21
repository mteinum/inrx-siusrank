namespace InrxToSiusRank.Tests;

public sealed class SiusRankOdfExportReaderTests
{
    [Fact]
    public void Parse_reads_rank_list_main_individual_results_with_shots()
    {
        using var file = TempFile.Create(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <OdfBody ResultStatus="INTERIM">
              <Competition Code="">
                <ExtendedHeader EventCode="SPRF_M" ShortName="HurtigFin_M" EventUnitName="25m Hurtigpistol Fin M" ProductType="IndividualResults" />
                <CumulativeResult Rank="1" ResultType="POINTS" Result="20" SortOrder="1">
                  <Competitor AccreditationNumber="1273763" Bib="26008" Organisation="NOR" NameDisplay="VRÅLSTAD Tore">
                    <Composition>
                      <Athlete Bib="26008" AccreditationNumber="1273763" FamilyName="VRÅLSTAD" GivenName="Tore">
                        <ExtendedResults>
                          <ExtendedResult Type="CER_SH" Code="SH_INNER_TENS" Value="1" />
                          <ExtendedResult Type="CER_SH" Pos="1" Code="SH_SHOT" Value="10">
                            <Extensions>
                              <Extension Type="SH_SHOT" Code="SH_TIMESTAMP" Value="2026-05-23T11:04:24.5300000" />
                              <Extension Type="SH_SHOT" Code="SH_SHOT_X" Value="-21.009" />
                              <Extension Type="SH_SHOT" Code="SH_SHOT_Y" Value="1223.656" />
                            </Extensions>
                          </ExtendedResult>
                          <ExtendedResult Type="CER_SH" Pos="2" Code="SH_SHOT" Value="10">
                            <Extensions>
                              <Extension Type="SH_SHOT" Code="SH_TIMESTAMP" Value="2026-05-23T11:04:25.5300000" />
                              <Extension Type="SH_SHOT" Code="SH_SHOT_X" Value="500" />
                              <Extension Type="SH_SHOT" Code="SH_SHOT_Y" Value="600" />
                            </Extensions>
                          </ExtendedResult>
                        </ExtendedResults>
                      </Athlete>
                    </Composition>
                  </Competitor>
                </CumulativeResult>
              </Competition>
            </OdfBody>
            """);

        var export = SiusRankOdfExportReader.Parse(file.Path);

        Assert.NotNull(export);
        Assert.Equal("HurtigFin_M", export.ShortName);
        Assert.Equal("SPRF_M", export.EventCode);
        Assert.Equal("IndividualResults", export.ProductType);
        var athlete = Assert.Single(export.Athletes);
        Assert.Equal("26008", athlete.BibNumber);
        Assert.Equal("1273763", athlete.AccreditationNumber);
        Assert.Equal(20, athlete.Result);
        Assert.Equal(1, athlete.InnerTens);
        Assert.Equal(2, athlete.Shots.Count);
        Assert.Equal(-0.21009m, athlete.Shots[0].X);
        Assert.Equal(12.23656m, athlete.Shots[0].Y);
    }

    [Fact]
    public void Parse_flattens_team_results_with_athlete_shots()
    {
        using var file = TempFile.Create(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <OdfBody ResultStatus="INTERIM">
              <Competition Code="">
                <ExtendedHeader EventCode="Silhuett_common" ShortName="Silhuett_common" EventUnitName="25m Silhuettpistol common" ProductType="TeamResults" />
                <CumulativeResult Rank="1" ResultType="POINTS" Result="1405-21x" SortOrder="1">
                  <Competitor Code="Arendal PK" Type="T" Organisation="NOR" NameDisplay="Arendal PK">
                    <Composition>
                      <Athlete Bib="26002" AccreditationNumber="26002" FamilyName="BARSTAD" GivenName="Lars Petter">
                        <ExtendedResults>
                          <ExtendedResult Type="CER_SH" Code="SH_INNER_TENS" Value="7" />
                          <ExtendedResult Type="CER_SH" Code="SH_GRANDTOTAL" Value="482-7x" />
                          <ExtendedResult Type="CER_SH" Pos="1" Code="SH_SHOT" Value="8">
                            <Extensions>
                              <Extension Type="SH_SHOT" Code="SH_TIMESTAMP" Value="2026-06-21T10:16:41.2600000" />
                              <Extension Type="SH_SHOT" Code="SH_SHOT_X" Value="-11825.015" />
                              <Extension Type="SH_SHOT" Code="SH_SHOT_Y" Value="4670.061" />
                            </Extensions>
                          </ExtendedResult>
                        </ExtendedResults>
                      </Athlete>
                      <Athlete Bib="26003" AccreditationNumber="26003" FamilyName="BARSTAD" GivenName="Elena">
                        <ExtendedResults>
                          <ExtendedResult Type="CER_SH" Code="SH_INNER_TENS" Value="6" />
                          <ExtendedResult Type="CER_SH" Code="SH_GRANDTOTAL" Value="477-6x" />
                          <ExtendedResult Type="CER_SH" Pos="1" Code="SH_SHOT" Value="10">
                            <Extensions>
                              <Extension Type="SH_SHOT" Code="SH_TIMESTAMP" Value="2026-06-21T10:16:42.2600000" />
                            </Extensions>
                          </ExtendedResult>
                        </ExtendedResults>
                      </Athlete>
                    </Composition>
                  </Competitor>
                </CumulativeResult>
              </Competition>
            </OdfBody>
            """);

        var export = SiusRankOdfExportReader.Parse(file.Path);

        Assert.NotNull(export);
        Assert.Equal("TeamResults", export.ProductType);
        Assert.Equal(2, export.Athletes.Count);
        Assert.Equal("26002", export.Athletes[0].BibNumber);
        Assert.Equal("BARSTAD Lars Petter", export.Athletes[0].NameForDisplay);
        Assert.Equal(482, export.Athletes[0].Result);
        Assert.Equal(7, export.Athletes[0].InnerTens);
        Assert.Equal("26003", export.Athletes[1].BibNumber);
        Assert.Equal("BARSTAD Elena", export.Athletes[1].NameForDisplay);
        Assert.Equal(477, export.Athletes[1].Result);
        Assert.Equal(6, export.Athletes[1].InnerTens);
    }

    private sealed class TempFile : IDisposable
    {
        private TempFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempFile Create(string contents)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.odf.xml");
            File.WriteAllText(path, contents);
            return new TempFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
