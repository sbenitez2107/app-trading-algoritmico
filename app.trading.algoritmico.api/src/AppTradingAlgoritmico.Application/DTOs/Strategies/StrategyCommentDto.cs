namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

public record StrategyCommentDto(Guid Id, string Content, DateTime CreatedAt, string? CreatedBy);
