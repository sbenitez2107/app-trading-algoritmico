namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>How a prop firm measures the maximum drawdown limit.</summary>
public enum DrawdownModel
{
    /// <summary>Limit measured against the fixed initial balance.</summary>
    Static = 0,
    /// <summary>Limit trails the highest equity/balance reached.</summary>
    Trailing = 1,
}
