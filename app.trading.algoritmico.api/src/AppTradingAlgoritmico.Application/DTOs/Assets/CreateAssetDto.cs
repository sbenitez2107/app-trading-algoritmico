using System.ComponentModel.DataAnnotations;

namespace AppTradingAlgoritmico.Application.DTOs.Assets;

public record CreateAssetDto(
    [Required] string Name,
    [Required] string Symbol
);
