using System.IO.Compression;
using System.Text;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for SqxParserService.ExtractStrategyMetadataAsync — indicator extraction:
/// EntryIndicators, PriceIndicators, and IndicatorParameters.
///
/// Semantic:
///   - Entry Indicators  = indicators inside &lt;signals&gt;. Two patterns supported:
///       * categoryType="indicator"   → use @key   (classic LinReg/SMA/RSI, XAUUSD LR strategies)
///       * categoryType="simpleRules" → use @mI    (bundled StdDevRising/ADXRising, DAX strategies)
///   - Price Indicators  = indicators inside entry-order price formulas under &lt;Then&gt;
///                         (Param key="#Price#" → Formula → Item). categories "indicator" + "priceValue".
///   - Indicator Params  = union of both sets, params from first occurrence, platform keys excluded.
///
/// All tests use inlined XML wrapped in a ZIP as settings.xml — no .sqx files on disk.
/// </summary>
public class SqxParserIndicatorsTests
{
    private readonly SqxParserService _sut = new();

    // ---------------------------------------------------------------------------
    // Classic pattern — <signals> with categoryType="indicator"
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_ClassicIndicatorsInSignals_ReturnedAsEntry()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="LinearRegression" display="LinReg" returnType="price" categoryType="indicator">
                <Param key="#Period#" type="int">28</Param>
                <Param key="#Shift#" type="int">1</Param>
              </Item>
            </signal>
            <signal variable="s2">
              <Item key="SMA" display="SMA" returnType="price" categoryType="indicator">
                <Param key="#Period#" type="int">20</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.EntryIndicators.Should().Be("LinearRegression, SMA");
        // No <Then>/#Price# formula → Price Indicators null
        result.PriceIndicators.Should().BeNull();
        result.IndicatorParameters.Should().Be("LinearRegression(Period=28, Shift=1); SMA(Period=20)");
    }

    // ---------------------------------------------------------------------------
    // DAX-style pattern — <signals> with categoryType="simpleRules" (mI attribute)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_SimpleRulesInSignals_UsesMiAttributeAsIndicatorName()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="StdDevRising" display="StdDev is rising" returnType="boolean" mI="StdDev" categoryType="simpleRules">
                <Param key="#Period#" type="int">88</Param>
                <Param key="#Shift#" type="int">1</Param>
              </Item>
              <Item key="ADXRising" display="ADX is rising" returnType="boolean" mI="ADX" categoryType="simpleRules">
                <Param key="#Period#" type="int">32</Param>
                <Param key="#Shift#" type="int">1</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        // Names come from @mI, not @key (StdDev not StdDevRising, ADX not ADXRising)
        result!.EntryIndicators.Should().Be("ADX, StdDev");
        result.PriceIndicators.Should().BeNull();
        result.IndicatorParameters.Should().Be("ADX(Period=32, Shift=1); StdDev(Period=88, Shift=1)");
    }

    // ---------------------------------------------------------------------------
    // Price Indicators — indicators inside <Then>/#Price# formula
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_IndicatorInsidePriceFormula_ReturnedAsPrice()
    {
        var xml = BuildSettingsXml(
            signalsInner: """
                <signal variable="s1">
                  <Item key="True" display="true" returnType="boolean" categoryType="other"/>
                </signal>
                """,
            thenInner: """
                <Item key="EnterAtStop" categoryType="other">
                  <Param key="#Price#" type="double">
                    <Formula key="SQ.Formulas.Price.UseFormula">
                      <Block key="#Value#">
                        <Item key="HighestInRange" display="HighestInRange" returnType="price" mI="BarRange" categoryType="indicator">
                          <Param key="#TimeFrom#" type="int">0</Param>
                          <Param key="#TimeTo#" type="int">1700</Param>
                          <Param key="#Shift#" type="int">1</Param>
                        </Item>
                      </Block>
                    </Formula>
                  </Param>
                </Item>
                """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.EntryIndicators.Should().BeNull();
        result.PriceIndicators.Should().Be("HighestInRange");
        result.IndicatorParameters.Should().Be("HighestInRange(TimeFrom=0, TimeTo=1700, Shift=1)");
    }

    [Fact]
    public async Task ExtractStrategyMetadata_PriceValueInPriceFormula_AlsoReturnedAsPrice()
    {
        // SessionHigh is categoryType="priceValue" — also counts as a price indicator.
        var xml = BuildSettingsXml(
            signalsInner: """
                <signal variable="s1">
                  <Item key="True" returnType="boolean" categoryType="other"/>
                </signal>
                """,
            thenInner: """
                <Item key="EnterAtStop" categoryType="other">
                  <Param key="#Price#" type="double">
                    <Formula key="SQ.Formulas.Price.UseFormula">
                      <Block key="#Value#">
                        <Item key="SessionHigh" display="SessionHigh" returnType="price" mI="Price" categoryType="priceValue">
                          <Param key="#StartHours#" type="int">13</Param>
                          <Param key="#EndHours#" type="int">4</Param>
                        </Item>
                      </Block>
                    </Formula>
                  </Param>
                </Item>
                """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.PriceIndicators.Should().Be("SessionHigh");
    }

    // ---------------------------------------------------------------------------
    // Combined — DAX-style full shape: simpleRules in signals + indicator in price formula
    // (matches real WF_6_22_GDAXI_H1_HIR_ADX_StdDEV structure)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_DaxLikeStrategy_ExtractsBothEntryAndPrice()
    {
        var xml = BuildSettingsXml(
            signalsInner: """
                <signal variable="s1">
                  <Item key="StdDevRising" returnType="boolean" mI="StdDev" categoryType="simpleRules">
                    <Param key="#Period#" type="int">88</Param>
                    <Param key="#Shift#" type="int">1</Param>
                  </Item>
                  <Item key="ADXRising" returnType="boolean" mI="ADX" categoryType="simpleRules">
                    <Param key="#Period#" type="int">32</Param>
                    <Param key="#Shift#" type="int">1</Param>
                  </Item>
                </signal>
                """,
            thenInner: """
                <Item key="EnterAtStop" categoryType="other">
                  <Param key="#Price#" type="double">
                    <Formula key="SQ.Formulas.Price.UseFormula">
                      <Block key="#Value#">
                        <Item key="HighestInRange" returnType="price" mI="BarRange" categoryType="indicator">
                          <Param key="#TimeFrom#" type="int">0</Param>
                          <Param key="#TimeTo#" type="int">1700</Param>
                          <Param key="#Shift#" type="int">1</Param>
                        </Item>
                      </Block>
                    </Formula>
                  </Param>
                </Item>
                """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.EntryIndicators.Should().Be("ADX, StdDev");
        result.PriceIndicators.Should().Be("HighestInRange");
        // Union of both sets, alphabetical, with each indicator's params
        result.IndicatorParameters.Should().Be(
            "ADX(Period=32, Shift=1); HighestInRange(TimeFrom=0, TimeTo=1700, Shift=1); StdDev(Period=88, Shift=1)");
    }

    // ---------------------------------------------------------------------------
    // No indicators → all null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_NoIndicators_ReturnsNullForAllIndicatorFields()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="True" display="true" returnType="boolean" categoryType="other"/>
            </signal>
            <signal variable="s2">
              <Item key="And" returnType="boolean" categoryType="operators">
                <Param key="#Left#" type="bool">true</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.EntryIndicators.Should().BeNull();
        result.PriceIndicators.Should().BeNull();
        result.IndicatorParameters.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Platform params (#Chart#, #Direction#, #Symbol#, #Size#) excluded from params output
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_ParametersFormatted_SkipsPlatformParams()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="LinearRegression" returnType="price" categoryType="indicator">
                <Param key="#Chart#" type="data">0</Param>
                <Param key="#Direction#" type="int">0</Param>
                <Param key="#Symbol#" type="string">EURUSD</Param>
                <Param key="#Size#" type="double">0.1</Param>
                <Param key="#Period#" type="int">28</Param>
                <Param key="#Shift#" type="int">1</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.IndicatorParameters.Should().Be("LinearRegression(Period=28, Shift=1)");
    }

    // ---------------------------------------------------------------------------
    // Duplicate indicator across signals → deduplicated
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_DuplicateIndicator_DeduplicatesInCsv()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="IsRising" returnType="boolean" categoryType="operators">
                <Block key="#Indicator#">
                  <Item key="LinearRegression" returnType="price" categoryType="indicator">
                    <Param key="#Period#" type="int">28</Param>
                    <Param key="#Shift#" type="int">1</Param>
                  </Item>
                </Block>
                <Param key="#Bars#" type="int">4</Param>
              </Item>
            </signal>
            <signal variable="s2">
              <Item key="IsRising" returnType="boolean" categoryType="operators">
                <Block key="#Indicator#">
                  <Item key="LinearRegression" returnType="price" categoryType="indicator">
                    <Param key="#Period#" type="int">28</Param>
                    <Param key="#Shift#" type="int">1</Param>
                  </Item>
                </Block>
                <Param key="#Bars#" type="int">4</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.EntryIndicators.Should().Be("LinearRegression");
        // #Bars# sits on IsRising (operator), not the indicator — excluded from the indicator's params.
        result.IndicatorParameters.Should().Be("LinearRegression(Period=28, Shift=1)");
    }

    // ---------------------------------------------------------------------------
    // Indicator with no tuning params → appears without parentheses
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractStrategyMetadata_IndicatorWithNoParams_AppearsWithoutParens()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="Close" returnType="price" categoryType="indicator">
                <Param key="#Chart#" type="data">0</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var result = await _sut.ExtractStrategyMetadataAsync(stream);

        result.Should().NotBeNull();
        result!.IndicatorParameters.Should().Be("Close");
    }

    // ---------------------------------------------------------------------------
    // ExtractPseudocodeAsync still works as thin wrapper
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractPseudocodeAsync_StillReturnsNonNull_AfterRefactor()
    {
        var xml = BuildSettingsXml(signalsInner: """
            <signal variable="s1">
              <Item key="SMA" returnType="price" categoryType="indicator">
                <Param key="#Period#" type="int">20</Param>
              </Item>
            </signal>
            """);

        using var stream = BuildSqxStream(xml);

        var pseudocode = await _sut.ExtractPseudocodeAsync(stream);

        pseudocode.Should().NotBeNullOrEmpty();
        pseudocode.Should().Contain("Engine:");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal settings.xml. thenInner is optional — when supplied,
    /// it is injected as the content of a Rule/Then block (for price-formula tests).
    /// </summary>
    private static string BuildSettingsXml(string signalsInner, string? thenInner = null)
    {
        var thenRule = string.IsNullOrEmpty(thenInner)
            ? string.Empty
            : $"""
                <Rule name="Long entry" type="IfThen" everyTick="false">
                  <If>
                    <Item key="BooleanVariable" returnType="boolean" categoryType="other"/>
                  </If>
                  <Then>
                    {thenInner}
                  </Then>
                </Rule>
              """;

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <StrategyFile Version="3.9.132">
              <Strategy name="TestStrategy" engine="MetaTrader4">
                <Rules>
                  <Events>
                    <Event key="OnBarUpdate">
                      <Rule name="Trading signals" type="Signal" everyTick="false">
                        <signals>
                          {signalsInner}
                        </signals>
                      </Rule>
                      {thenRule}
                    </Event>
                  </Events>
                </Rules>
              </Strategy>
            </StrategyFile>
            """;
    }

    /// <summary>Creates a .sqx stream (ZIP containing settings.xml) from the given XML string.</summary>
    private static MemoryStream BuildSqxStream(string settingsXml)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("settings.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(settingsXml);
        }
        stream.Position = 0;
        return stream;
    }
}
