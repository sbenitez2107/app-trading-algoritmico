namespace AppTradingAlgoritmico.Domain.Backtests;

/// <summary>
/// The lot grid a backtest was sized on: the quantum every <c>Size</c> lands on, plus the floor and
/// ceiling the broker/platform enforces (design.md D8).
/// <para>
/// It is a RECORD, not a set of constants, and that is deliberate. The 1-decimal grid
/// (<c>step 0.10</c>) is the one that exposes the minimum-lot overshoot, so it must be
/// CONSTRUCTIBLE in a test without the system shipping support for it. Constants would have forced
/// the choice between shipping a grid nobody uses and being unable to measure the failure mode.
/// </para>
/// <para>
/// <b><see cref="MinLot"/> is the step</b>, not <c>0.1</c>. The <c>0.1</c> in the Money Management
/// notes is <i>Size if no MM</i> — the fallback used when money management is OFF — not a floor.
/// The evidence is the two committed exports: the 1-decimal one pins 114 of 337 trades (33.8%) at
/// <c>0.1</c>, which is its own step, against 1 of 329 (0.3%) on the 2-decimal grid. Per-symbol
/// broker minimums are deferred — no data source for them exists yet.
/// </para>
/// </summary>
public sealed record LotGrid
{
    /// <summary>
    /// Validating constructor — an invalid grid is unrepresentable rather than merely unlikely.
    /// </summary>
    /// <param name="sizeDecimals">Decimal places <paramref name="step"/> needs; it must need exactly this many.</param>
    /// <param name="step">The size quantum. Every achievable <c>Size</c> is an integral multiple of it.</param>
    /// <param name="minLot">Smallest tradable size. Never below <paramref name="step"/> — a size off the grid is not tradable.</param>
    /// <param name="maxLots">Largest tradable size.</param>
    public LotGrid(int sizeDecimals, decimal step, decimal minLot, decimal maxLots)
    {
        if (step <= 0m)
            throw new ArgumentOutOfRangeException(nameof(step), step, "Step must be greater than zero.");

        var requiredDecimals = DecimalPlaces(step);
        if (sizeDecimals != requiredDecimals)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeDecimals),
                sizeDecimals,
                $"Step {step} needs exactly {requiredDecimals} decimal place(s); the grid declares {sizeDecimals}.");
        }

        if (minLot < step)
            throw new ArgumentOutOfRangeException(nameof(minLot), minLot, $"MinLot must be at least the step {step}.");

        if (maxLots < minLot)
            throw new ArgumentOutOfRangeException(nameof(maxLots), maxLots, $"MaxLots must be at least MinLot {minLot}.");

        SizeDecimals = sizeDecimals;
        Step = step;
        MinLot = minLot;
        MaxLots = maxLots;
    }

    /// <summary>Decimal places the <see cref="Step"/> requires — exactly, not merely at least.</summary>
    public int SizeDecimals { get; }

    /// <summary>The size quantum. Rounding onto it is FLOOR in both directions of the pipeline (D3).</summary>
    public decimal Step { get; }

    /// <summary>Smallest tradable size. A trade pinned here is <c>Unbounded</c> ABOVE, never <c>Imputed</c> (D5).</summary>
    public decimal MinLot { get; }

    /// <summary>Largest tradable size. A trade pinned here is <c>Unbounded</c> BELOW (D5).</summary>
    public decimal MaxLots { get; }

    /// <summary>
    /// The grid the two committed XAUUSD exports were produced on: 2 decimals, step and minimum lot
    /// both <c>0.01</c>, ceiling <c>10</c>.
    /// </summary>
    public static LotGrid ImoxRetester { get; } = new(sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m);

    /// <summary>
    /// Decimal places a value genuinely needs, ignoring trailing zeros — <c>0.10m</c> needs one,
    /// <c>0.05m</c> needs two. Trailing zeros are why this cannot read <c>decimal.GetBits</c>'s
    /// scale directly: <c>0.10m</c> is stored with scale 2.
    /// </summary>
    private static int DecimalPlaces(decimal value)
    {
        var places = 0;
        var scaled = value;

        while (scaled != decimal.Truncate(scaled) && places < 28)
        {
            scaled *= 10m;
            places++;
        }

        return places;
    }
}
