using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Unit tests for the MT4 trade import endpoint — spec: mt-trade-import R1.
/// Route: POST api/trading-accounts/{id}/trades/import.
/// Covers routing, status codes, and error mapping only.
/// Service logic is covered by TradeImportServiceTests.
/// </summary>
public class TradingAccountsControllerImportTests
{
    private static TradeImportResultDto MakeImportResult() => new(
        Imported: 5,
        Updated: 2,
        Skipped: 1,
        Orphans: [],
        Snapshot: new SnapshotDto(
            ReportTime: new DateTime(2026, 4, 22),
            Balance: 100_000m,
            Equity: 100_500m,
            FloatingPnL: 500m,
            Margin: 1_000m,
            FreeMargin: 99_000m,
            ClosedTradePnL: 2_897m,
            Currency: "USD"));

    private static IFormFile CreateMockFormFile(string name, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(name);
        fileMock.Setup(f => f.Length).Returns(bytes.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        return fileMock.Object;
    }

    private static TradingAccountsController CreateSut(Mock<ITradeImportService> tradeMock) =>
        new(
            Mock.Of<ITradingAccountService>(),
            tradeMock.Object,
            Mock.Of<ILogger<TradingAccountsController>>());

    [Fact]
    public async Task ImportTrades_ValidFile_Returns200WithResultDto()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var expectedResult = MakeImportResult();

        var tradeMock = new Mock<ITradeImportService>();
        tradeMock
            .Setup(s => s.ImportAsync(accountId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var sut = CreateSut(tradeMock);
        var file = CreateMockFormFile("report.htm", "<html/>");

        // Act — spec R1 scenario 1
        var result = await sut.ImportTrades(accountId, file, default);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var dto = okResult.Value as TradeImportResultDto;
        dto!.Imported.Should().Be(5);
    }

    [Fact]
    public async Task ImportTrades_AccountNotFound_Returns404()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var tradeMock = new Mock<ITradeImportService>();
        tradeMock
            .Setup(s => s.ImportAsync(accountId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("account not found"));

        var sut = CreateSut(tradeMock);
        var file = CreateMockFormFile("report.htm", "<html/>");

        // Act — spec R1 scenario 3
        var result = await sut.ImportTrades(accountId, file, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ImportTrades_UnparseableHtml_Returns400()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var tradeMock = new Mock<ITradeImportService>();
        tradeMock
            .Setup(s => s.ImportAsync(accountId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("HTML could not be parsed."));

        var sut = CreateSut(tradeMock);
        var file = CreateMockFormFile("report.htm", "garbage");

        // Act — spec R1 scenario 4
        var result = await sut.ImportTrades(accountId, file, default);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
