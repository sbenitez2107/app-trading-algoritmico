using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.BuildingBlocks;

public record BuildingBlockDto(
    Guid Id,
    string Name,
    string? Description,
    BuildingBlockType Type,
    DateTime CreatedAt
);
