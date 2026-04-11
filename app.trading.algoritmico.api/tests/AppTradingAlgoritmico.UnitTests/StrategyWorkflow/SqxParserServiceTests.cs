using System.IO.Compression;
using System.Text;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

public class SqxParserServiceTests
{
    private readonly SqxParserService _sut = new();

    [Fact]
    public async Task ParseZipAsync_WithValidSqxFiles_ReturnsStrategies()
    {
        // Arrange
        using var zipStream = CreateTestZipWithSqxFiles(["Strategy_1", "Strategy_2"]);

        // Act
        var result = await _sut.ParseZipAsync(zipStream);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Strategy_1");
        result[1].Name.Should().Be("Strategy_2");
    }

    [Fact]
    public async Task ParseZipAsync_WithNoSqxFiles_ReturnsEmpty()
    {
        // Arrange
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("Not a strategy file");
        }
        zipStream.Position = 0;

        // Act
        var result = await _sut.ParseZipAsync(zipStream);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseZipAsync_ExtractsPseudocodeFromSettingsXml()
    {
        // Arrange
        using var zipStream = CreateTestZipWithSqxFiles(["TestStrategy"]);

        // Act
        var result = await _sut.ParseZipAsync(zipStream);

        // Assert
        result.Should().HaveCount(1);
        result[0].Pseudocode.Should().NotBeNullOrEmpty();
        result[0].Pseudocode.Should().Contain("Engine: MetaTrader4");
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

    /// <summary>Creates a test ZIP containing .sqx files (each .sqx is itself a ZIP with settings.xml).</summary>
    private static MemoryStream CreateTestZipWithSqxFiles(string[] strategyNames)
    {
        var outerStream = new MemoryStream();
        using (var outerArchive = new ZipArchive(outerStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in strategyNames)
            {
                // Create inner .sqx ZIP
                using var innerStream = new MemoryStream();
                using (var innerArchive = new ZipArchive(innerStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var settingsEntry = innerArchive.CreateEntry("settings.xml");
                    using var writer = new StreamWriter(settingsEntry.Open(), Encoding.UTF8);
                    writer.Write(CreateMinimalSettingsXml(name));
                }
                innerStream.Position = 0;

                // Add inner ZIP as .sqx entry in outer ZIP
                var sqxEntry = outerArchive.CreateEntry($"{name}.sqx");
                using var sqxWriter = sqxEntry.Open();
                innerStream.CopyTo(sqxWriter);
            }
        }
        outerStream.Position = 0;
        return outerStream;
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
