namespace AppTradingAlgoritmico.Application.DTOs.GridPresets;

public record GridPresetDto(
    Guid Id,
    string Name,
    string[] VisibleColumns,
    string[] ColumnOrder,
    DateTime CreatedAt
);

public record CreateGridPresetDto(
    string Name,
    string[] VisibleColumns,
    string[] ColumnOrder
);
