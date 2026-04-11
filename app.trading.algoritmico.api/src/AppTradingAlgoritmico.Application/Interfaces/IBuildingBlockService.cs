using AppTradingAlgoritmico.Application.DTOs.BuildingBlocks;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IBuildingBlockService
{
    Task<IEnumerable<BuildingBlockDto>> GetAllAsync(CancellationToken ct = default);
    Task<BuildingBlockDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BuildingBlockDto> CreateAsync(CreateBuildingBlockDto dto, Stream? sqbFile, CancellationToken ct = default);
    Task<BuildingBlockDto> UpdateAsync(Guid id, CreateBuildingBlockDto dto, Stream? sqbFile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
