using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// The correlation matrix over a group of BACKTEST-derived dated series (design.md D6).
/// <para>
/// <b>Aligned on the pairwise INTERSECTION</b>, unlike the real-account path, which keeps its
/// shipped union alignment bit-identically. A union-aligned coefficient over a series that is 91.8%
/// zero-net days does not weakly measure co-movement — it measures CO-ABSENCE. Intersection removes
/// that bias from the number instead of disclosing it, so no co-absence caveat applies to a cell
/// here.
/// </para>
/// <para>
/// <b>Cells are <c>decimal?</c></b> and a cell is WITHHELD, never <c>0</c>, when the pair has fewer
/// than two co-active days (Pearson's own domain) or either member's series is constant over the
/// intersection — the shipped <c>Pearson</c> returns <c>0</c> for both, and a published <c>0</c>
/// reads as "uncorrelated", a different claim from "undefined". That mapping lives in the backtest
/// adapter, so no shipped behaviour moves.
/// </para>
/// </summary>
/// <param name="Labels">Member labels, in matrix order.</param>
/// <param name="Matrix">Square, symmetric, diagonal <c>1</c>. A null cell is withheld.</param>
/// <param name="CoActiveDays">
/// Per cell, the days on which BOTH members closed a trade — reported with NO invented
/// minimum-overlap threshold, because nothing measured supports one. A pair with 3 co-active days
/// gets a reported coefficient with <c>3</c> beside it, and the reader judges.
/// </param>
/// <param name="CoActiveShare">
/// Per cell, <c>CoActiveDays</c> over the days on which EITHER member closed a trade. The
/// diagonal is <c>1</c>.
/// </param>
/// <param name="ObservationDays">
/// Days on which ANY member closed a trade — it describes the sample the matrix was drawn from,
/// not any single pair's overlap. Read <see cref="CoActiveDays"/> for that.
/// </param>
/// <param name="AverageCorrelation">
/// Mean of the REPORTED off-diagonal cells only, or null when every one is withheld. Never
/// <c>0</c>: an average over nothing is not zero correlation.
/// </param>
/// <param name="WithheldCellCount">Distinct member PAIRS whose cell is withheld — counted once per pair, not twice.</param>
/// <param name="Alignment">Always <c>"Intersection"</c> here. Stated on the payload so the reader never has to infer it.</param>
/// <param name="Segment">Which sample the matrix was computed over.</param>
/// <param name="Density">The merged group series' density — see <see cref="SeriesDensityDto"/> for its mixed provenance.</param>
public sealed record BacktestCorrelationDto(
    IReadOnlyList<string> Labels,
    IReadOnlyList<IReadOnlyList<decimal?>> Matrix,
    IReadOnlyList<IReadOnlyList<int>> CoActiveDays,
    IReadOnlyList<IReadOnlyList<decimal>> CoActiveShare,
    int ObservationDays,
    decimal? AverageCorrelation,
    int WithheldCellCount,
    string Alignment,
    BacktestSegment Segment,
    SeriesDensityDto Density);
