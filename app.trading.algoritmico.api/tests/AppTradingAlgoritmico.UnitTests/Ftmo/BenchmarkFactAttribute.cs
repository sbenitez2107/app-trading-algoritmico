namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.1.2 (design.md Decision 1; hard rule 7) — a <see cref="FactAttribute"/>
/// that is SKIPPED unless the <c>FTMO_BENCH</c> environment variable is set to <c>1</c>. xUnit reads
/// <see cref="Skip"/> at discovery/execution time, so evaluating it here is a real, runtime skip — not
/// a vacuous pass. A CI run without <c>FTMO_BENCH=1</c> never asserts this gate at all.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchmarkFactAttribute : FactAttribute
{
    public override string? Skip
    {
        get => Environment.GetEnvironmentVariable("FTMO_BENCH") == "1"
            ? null
            : "Benchmark gate — set FTMO_BENCH=1 to run (design.md Decision 1, hard rule 7).";
        set { }
    }
}
