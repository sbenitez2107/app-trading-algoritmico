using System.IO.Compression;
using System.Text;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

public class SqxParserServiceTests
{
    private readonly SqxParserService _sut = new();

    [Fact]
    public async Task ExtractPseudocodeAsync_WithValidSqxFile_ReturnsPseudocode()
    {
        // Arrange
        using var sqxStream = CreateTestSqxStream("Strategy_1");

        // Act
        var result = await _sut.ExtractPseudocodeAsync(sqxStream);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Engine: MetaTrader4");
    }

    [Fact]
    public async Task ExtractPseudocodeAsync_WithNoSettingsXml_ReturnsNull()
    {
        // Arrange — sqx that has no settings.xml inside
        using var sqxStream = new MemoryStream();
        using (var archive = new ZipArchive(sqxStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("other.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("<data/>");
        }
        sqxStream.Position = 0;

        // Act
        var result = await _sut.ExtractPseudocodeAsync(sqxStream);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractPseudocodeAsync_WithInvalidStream_ReturnsNull()
    {
        // Arrange — not a zip file
        var bytes = Encoding.UTF8.GetBytes("not a zip");
        using var stream = new MemoryStream(bytes);

        // Act
        var result = await _sut.ExtractPseudocodeAsync(stream);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseSqbConfigAsync_ExtractsXml()
    {
        // Arrange
        using var sqbStream = CreateTestSqbFile("<Blocks><BuildingBlocks/></Blocks>");

        // Act
        var result = await _sut.ParseSqbConfigAsync(sqbStream);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("BuildingBlocks");
    }

    [Fact]
    public async Task ParseSqbConfigAsync_NoConfigXml_ReturnsNull()
    {
        // Arrange
        using var sqbStream = new MemoryStream();
        using (var archive = new ZipArchive(sqbStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("other.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("<data/>");
        }
        sqbStream.Position = 0;

        // Act
        var result = await _sut.ParseSqbConfigAsync(sqbStream);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>Creates a test .sqx stream (a ZIP containing settings.xml).</summary>
    private static MemoryStream CreateTestSqxStream(string strategyName)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var settingsEntry = archive.CreateEntry("settings.xml");
            using var writer = new StreamWriter(settingsEntry.Open(), Encoding.UTF8);
            writer.Write(CreateMinimalSettingsXml(strategyName));
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateTestSqbFile(string xmlContent)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("config.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(xmlContent);
        }
        stream.Position = 0;
        return stream;
    }

    private static string CreateMinimalSettingsXml(string name) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <StrategyFile Version="3.9.132">
          <Strategy name="{name}" engine="MetaTrader4">
            <MoneyManagement type="FixedSize">
              <params><Param key="#Size#" type="double">0.1</Param></params>
            </MoneyManagement>
            <GlobalSLPT>
              <useSameSLPTforBothDirections>true</useSameSLPTforBothDirections>
              <values>
                <globalSL><values type="fixed"><value>50</value></values></globalSL>
                <globalPT><values type="fixed"><value>100</value></values></globalPT>
              </values>
            </GlobalSLPT>
            <Rules>
              <Events>
                <Event key="OnBarUpdate">
                  <Rule name="Trading signals" type="Signal" everyTick="false">
                    <signals>
                      <signal variable="test-signal">
                        <Item key="RSIOverbought" name="RSI Overbought" display="RSI(14) > 70" returnType="boolean">
                          <Param key="#Period#" type="int">14</Param>
                          <Param key="#Level#" type="double">70</Param>
                        </Item>
                      </signal>
                    </signals>
                  </Rule>
                </Event>
              </Events>
            </Rules>
          </Strategy>
        </StrategyFile>
        """;
}
