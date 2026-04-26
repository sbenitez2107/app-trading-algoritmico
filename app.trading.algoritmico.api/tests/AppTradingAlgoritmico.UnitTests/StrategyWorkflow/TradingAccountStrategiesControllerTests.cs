using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for TradingAccountStrategiesController — spec: account-strategies R1, R2.
/// Tests map to controller-level concerns only (routing, status codes, error mapping).
/// Service logic is covered by StrategyService tests.
/// </summary>
public class TradingAccountStrategiesControllerTests
{
    private static StrategyDto MakeStrategyDto() => new(
        Guid.NewGuid(), "Strategy1", null,
        null, null, null, // EntryIndicators, PriceIndicators, IndicatorParameters
        null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null,
        null, null, null, null, null,
        DateTime.UtcNow,
        null // MagicNumber
    );

    [Fact]
    public async Task GetStrategies_ExistingAccount_Returns200WithPagedResult()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var pagedResult = new PagedResult<StrategyDto>([MakeStrategyDto(), MakeStrategyDto()], 2, 1, 20);

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.GetByAccountAsync(accountId, 1, 20, default))
                   .ReturnsAsync(pagedResult);

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act
        var result = await sut.GetStrategies(accountId, 1, 20, default);

        // Assert — spec R1 scenario 1
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var dto = okResult.Value as PagedResult<StrategyDto>;
        dto!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetStrategies_AccountNotFound_Returns404()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.GetByAccountAsync(accountId, 1, 20, default))
                   .ThrowsAsync(new KeyNotFoundException("not found"));

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act — spec R1 scenario 3
        var result = await sut.GetStrategies(accountId, 1, 20, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PostStrategy_ValidFiles_Returns201WithStrategyDto()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var dto = MakeStrategyDto();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddToAccountAsync(accountId, "Test", It.IsAny<Stream>(), It.IsAny<Stream>(), null, default))
                   .ReturnsAsync(dto);

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        var sqxFile = CreateMockFormFile("test.sqx", "sqx content");
        var htmlFile = CreateMockFormFile("test.html", "<html/>");

        // Act — spec R2 scenario 1
        var result = await sut.CreateStrategy(accountId, "Test", sqxFile, htmlFile, magicNumber: null, default);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task PostStrategy_WithMagicNumber_PassesMagicNumberToService()
    {
        // Arrange — spec R-M2: magicNumber form field is forwarded to the service
        var accountId = Guid.NewGuid();
        var dto = MakeStrategyDto();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddToAccountAsync(
                        accountId, "Test", It.IsAny<Stream>(), It.IsAny<Stream>(), 2333376, default))
                   .ReturnsAsync(dto)
                   .Verifiable();

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act
        var result = await sut.CreateStrategy(
            accountId, "Test",
            CreateMockFormFile("test.sqx", "x"),
            CreateMockFormFile("test.html", "<html/>"),
            magicNumber: 2333376,
            default);

        // Assert
        (result.Result as CreatedAtActionResult)!.StatusCode.Should().Be(201);
        serviceMock.Verify();
    }

    [Fact]
    public async Task PostStrategy_MissingSqxFile_Returns400()
    {
        // Arrange
        var serviceMock = new Mock<IStrategyService>();
        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act — spec R2 missing sqx scenario
        var result = await sut.CreateStrategy(Guid.NewGuid(), "Test", sqxFile: null, htmlFile: CreateMockFormFile("test.html", "<html/>"), magicNumber: null, default);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostStrategy_MissingHtmlFile_Returns400()
    {
        // Arrange
        var serviceMock = new Mock<IStrategyService>();
        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act — spec R2 missing html scenario
        var result = await sut.CreateStrategy(Guid.NewGuid(), "Test", sqxFile: CreateMockFormFile("test.sqx", "content"), htmlFile: null, magicNumber: null, default);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostStrategy_UnparseableHtml_Returns400()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddToAccountAsync(accountId, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<int?>(), default))
                   .ThrowsAsync(new ArgumentException("Invalid SQX HTML report."));

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act — spec R2 scenario 4
        var result = await sut.CreateStrategy(accountId, "Test", CreateMockFormFile("test.sqx", "x"), CreateMockFormFile("test.html", "bad"), magicNumber: null, default);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostStrategy_AccountNotFound_Returns404()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddToAccountAsync(accountId, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<int?>(), default))
                   .ThrowsAsync(new KeyNotFoundException("account not found"));

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act — spec R2 scenario 5
        var result = await sut.CreateStrategy(accountId, "Test", CreateMockFormFile("test.sqx", "x"), CreateMockFormFile("test.html", "<html/>"), magicNumber: null, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AssignMagicNumber_HappyPath_Returns200WithStrategyDto()
    {
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var dto = MakeStrategyDto();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AssignMagicNumberAsync(accountId, strategyId, 4242, default))
                   .ReturnsAsync(dto);

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        var result = await sut.AssignMagicNumber(accountId, strategyId, new AssignMagicNumberDto(4242), default);

        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task AssignMagicNumber_StrategyNotFound_Returns404()
    {
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AssignMagicNumberAsync(accountId, strategyId, It.IsAny<int>(), default))
                   .ThrowsAsync(new KeyNotFoundException("not found"));

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        var result = await sut.AssignMagicNumber(accountId, strategyId, new AssignMagicNumberDto(1), default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AssignMagicNumber_Conflict_Returns409()
    {
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AssignMagicNumberAsync(accountId, strategyId, It.IsAny<int>(), default))
                   .ThrowsAsync(new InvalidOperationException("magic in use"));

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        var result = await sut.AssignMagicNumber(accountId, strategyId, new AssignMagicNumberDto(1), default);

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

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
}
