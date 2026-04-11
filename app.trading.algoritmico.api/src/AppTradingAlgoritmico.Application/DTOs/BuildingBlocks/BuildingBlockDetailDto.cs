using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.BuildingBlocks;

public record BuildingBlockDetailDto(
    Guid Id,
    string Name,
    string? Description,
    BuildingBlockType Type,
    string? XmlConfig,
    DateTime CreatedAt
);
