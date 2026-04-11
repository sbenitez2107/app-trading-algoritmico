using System.ComponentModel.DataAnnotations;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.BuildingBlocks;

public record CreateBuildingBlockDto(
    [Required] string Name,
    string? Description,
    [Required] BuildingBlockType Type
);
