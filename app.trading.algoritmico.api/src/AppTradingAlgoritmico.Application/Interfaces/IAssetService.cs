using AppTradingAlgoritmico.Application.DTOs.Assets;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IAssetService
{
    Task<IEnumerable<AssetDto>> GetAllAsync(CancellationToken ct = default);
    Task<AssetDto> CreateAsync(CreateAssetDto dto, CancellationToken ct = default);
}
