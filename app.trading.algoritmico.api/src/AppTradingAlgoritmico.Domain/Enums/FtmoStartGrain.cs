namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// The grain multi-start enumeration replays at (ftmo-multi-start, spec.md "Starts Are Enumerated At
/// Monthly Grain From The Data"). Only <see cref="Monthly"/> exists — weekly and every-trade grains are
/// rejected alternatives (proposal.md Decision D1), never implemented.
/// </summary>
public enum FtmoStartGrain
{
    /// <summary>One start per FTMO (Berlin) calendar month. Also the CLR default.</summary>
    Monthly = 0,
}
