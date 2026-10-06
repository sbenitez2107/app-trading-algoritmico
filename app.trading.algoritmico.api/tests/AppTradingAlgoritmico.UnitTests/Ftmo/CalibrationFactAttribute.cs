namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search D9 (hard rule 7) — a <see cref="FactAttribute"/> that is SKIPPED unless the
/// <c>FTMO_CALIBRATION_CONNECTION</c> environment variable holds a connection string. Running it touches a REAL
/// database (read-only), so it needs explicit user authorization naming the target; a CI run never sets the variable
/// and so never executes it. Like <see cref="BenchmarkFactAttribute"/>, the skip is a real runtime skip, not a vacuous pass.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CalibrationFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "FTMO_CALIBRATION_CONNECTION";

    public override string? Skip
    {
        get => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable))
            ? $"Calibration against a real database — set {ConnectionVariable} (needs explicit user authorization; read-only)."
            : null;
        set { }
    }
}
