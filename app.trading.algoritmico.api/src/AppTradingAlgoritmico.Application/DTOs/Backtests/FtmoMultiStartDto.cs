namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// ftmo-multi-start PR2, task 2.3 (design.md Decision 7) — nearest-rank order statistics over an
/// integer sample (days to some event). <c>N</c> is the observation count; every quantile is
/// <c>null</c> only when <c>N</c> is 0.
/// <para>
/// <strong>Apply-time deviation (flagged, not silent):</strong> design.md's File Changes table places
/// this file's whole DTO group (<see cref="FtmoMultiStartDto"/> and its siblings) in PR4, alongside
/// <c>FtmoMultiStartReadService.cs</c>. This one record is pulled forward into PR2 instead, because
/// <c>FtmoOrderStatistics.Compute</c> (task 2.3.4) is spec'd to return exactly this shape and this
/// record has no dependency on the PR4 service, DI, controller, or any other PR4 file — pulling it
/// forward changes no PR4 file count or scope. Confirm before PR4 starts that PR4 simply adds its
/// sibling records (<c>FtmoMultiStartDto</c>, <c>FtmoMultiStartRunDto</c>, etc.) to this same file
/// rather than re-declaring <see cref="FtmoOrderStatisticsDto"/>.
/// </para>
/// </summary>
/// <param name="N">Observation count.</param>
/// <param name="Min">Nearest-rank p=0 (the smallest observed value); <c>null</c> only when <c>N</c> is 0.</param>
/// <param name="Q1">Nearest-rank p=0.25.</param>
/// <param name="Median">Nearest-rank p=0.5.</param>
/// <param name="Q3">Nearest-rank p=0.75.</param>
/// <param name="Max">Nearest-rank p=1 (the largest observed value).</param>
public sealed record FtmoOrderStatisticsDto(int N, int? Min, int? Q1, int? Median, int? Q3, int? Max);
