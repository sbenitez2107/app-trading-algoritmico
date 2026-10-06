using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AppTradingAlgoritmico.Infrastructure.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>Everything the ground truth depends on; two runs with an equal fingerprint share one truth.</summary>
internal sealed record TruthFingerprint(
    string Account, decimal Risk, decimal Capital, decimal FxLow, decimal FxHigh, int MaxPerInstrument,
    IReadOnlyList<string> EligibleStrategyIds, int K4Stride, IReadOnlyList<string> SampleKeys);

/// <summary>ftmo-group-search D9 — the pure helpers of the calibration run (test-only support).</summary>
internal static class FtmoGroupSearchCalibration
{
    /// <summary>
    /// The ground-truth sample over the surviving candidates in enumeration order: every candidate of size 2 and 3, and
    /// every <paramref name="stride"/>-th candidate of each larger size (counted within that size). Deterministic, no RNG.
    /// <para>Known bias (k = 4): the stride is systematic over a lexicographic enumeration, so it correlates with the
    /// leading member indices rather than being a random draw, and the k = 4 "truth" is the top of the SAMPLE, not of
    /// all k = 4 candidates: an unsampled true top candidate is invisible. k = 4 recall therefore measures agreement with
    /// a 1-in-stride slice and can overstate the shortlist's recall; k = 2 and k = 3 are exhaustive and unbiased.</para>
    /// </summary>
    internal static IReadOnlyList<int[]> GroundTruthSample(IReadOnlyList<int[]> survivors, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, 1);

        var seen = new Dictionary<int, int>();
        var sample = new List<int[]>();
        foreach (var combo in survivors)
        {
            var index = seen.GetValueOrDefault(combo.Length);
            seen[combo.Length] = index + 1;
            if (combo.Length <= 3 || index % stride == 0)
                sample.Add(combo);
        }

        return sample;
    }

    /// <summary>
    /// <c>|topK(truth) ∩ shortlist| / K</c>. When fewer than K candidates are ranked the denominator is that smaller
    /// count, so a sample smaller than K does not read as a recall below 1. An empty ranking has recall 0.
    /// </summary>
    internal static decimal RecallAtK(IReadOnlyList<string> truthRanked, IReadOnlySet<string> shortlist, int k)
        => RecallAtK(truthRanked, s => s, static (_, _) => false, shortlist, k);

    /// <summary>
    /// Tie-robust recall: a candidate past position K that ties with the K-th entry (per <paramref name="ties"/>, the
    /// full ranking key except the id tie-break) also counts as "in the top K", so an arbitrary id order at the boundary
    /// cannot lower recall. Hits are capped at the denominator, so the result stays in [0, 1].
    /// </summary>
    internal static decimal RecallAtK<T>(
        IReadOnlyList<T> truthRanked, Func<T, string> key, Func<T, T, bool> ties, IReadOnlySet<string> shortlist, int k)
    {
        var n = Math.Min(k, truthRanked.Count);
        if (n == 0)
            return 0m;

        var boundary = truthRanked[n - 1];
        var top = truthRanked.Take(n).Concat(truthRanked.Skip(n).TakeWhile(t => ties(boundary, t)));
        return (decimal)Math.Min(n, top.Count(t => shortlist.Contains(key(t)))) / n;
    }

    /// <summary>
    /// The minimum proxy-rank depth (1-based) at which the first <paramref name="depth"/> entries of
    /// <paramref name="proxyOrder"/> hold at least <paramref name="fraction"/> of the true top K, using the same
    /// tie-robust truth as <see cref="RecallAtK{T}"/> (candidates tying with the K-th entry join the top set; hits are
    /// capped at the denominator <c>n = min(K, truth count)</c>, and the target is <c>ceil(fraction * n)</c>).
    /// Returns 0 when there is nothing to hold, and null when the proxy order never reaches the target (a true-top
    /// candidate absent from the order, e.g. removed by the proxy, can never be held).
    /// </summary>
    internal static int? DepthToHold<T>(
        IReadOnlyList<string> proxyOrder, IReadOnlyList<T> truthRanked, Func<T, string> key, Func<T, T, bool> ties,
        int k, decimal fraction)
    {
        var n = Math.Min(k, truthRanked.Count);
        if (n == 0)
            return 0;

        var boundary = truthRanked[n - 1];
        var top = truthRanked.Take(n).Concat(truthRanked.Skip(n).TakeWhile(t => ties(boundary, t)))
            .Select(key).ToHashSet();
        var target = (int)Math.Ceiling(fraction * n);
        if (target <= 0)
            return 0;

        var hits = 0;
        for (var i = 0; i < proxyOrder.Count; i++)
        {
            if (top.Contains(proxyOrder[i]) && ++hits >= target)
                return i + 1;
        }

        return null;
    }

    internal const decimal DefaultFxLow = 1.05m;
    internal const decimal DefaultFxHigh = 1.20m;

    /// <summary>The optional FX band (<c>FTMO_CALIBRATION_FX_LOW</c> / <c>_HIGH</c>); each side defaults when unset or blank.</summary>
    internal static (decimal Low, decimal High) ResolveFxBand(string? low, string? high)
        => (string.IsNullOrWhiteSpace(low) ? DefaultFxLow : decimal.Parse(low, CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(high) ? DefaultFxHigh : decimal.Parse(high, CultureInfo.InvariantCulture));

    /// <summary>The per-symbol strategy count, ordered by symbol, e.g. <c>NQ=3 XAUUSD=2</c>; a missing symbol reads <c>(none)</c>.</summary>
    internal static string SymbolCounts(IEnumerable<string?> symbols)
        => string.Join(" ", symbols.GroupBy(s => s ?? "(none)", StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Count()}"));

    /// <summary>
    /// True when the shortlist cannot be told apart from the survivor set (it holds every survivor, or the survivors fit
    /// in the shortlist), so recall is 1.0 by construction and the calibration demonstrates nothing.
    /// </summary>
    internal static bool IsVacuous(int survivors, int shortlist, int shortlistSize)
        => survivors <= shortlistSize || shortlist == survivors;

    internal static string VacuousMessage(int survivors, int shortlist, int shortlistSize)
        => $"VACUOUS CALIBRATION: the shortlist holds every survivor (survivors={survivors}, shortlist={shortlist}, "
            + $"ShortlistSize={shortlistSize}), so recall is 1.0 by construction and nothing is demonstrated. "
            + "Widen the pool (FX band, max per instrument, account) until survivors exceed the shortlist.";

    // ---- the persisted ground truth (FTMO_CALIBRATION_TRUTH_FILE) ----

    /// <summary>Bump whenever the persisted shape (or the meaning of an entry) changes, so an old file is refused.</summary>
    internal const int TruthSchemaVersion = 1;

    private sealed record TruthFile(int SchemaVersion, TruthFingerprint Fingerprint, IReadOnlyList<FtmoGroupSearchRanking.RankEntry> Entries);

    private static readonly System.Text.Json.JsonSerializerOptions TruthJson = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    /// <summary>Builds the fingerprint; the id and sample-key lists are sorted (ordinal), so enumeration order is irrelevant.</summary>
    internal static TruthFingerprint BuildFingerprint(
        string account, decimal risk, decimal capital, decimal fxLow, decimal fxHigh, int maxPerInstrument,
        IEnumerable<Guid> eligibleIds, int k4Stride, IEnumerable<string> sampleKeys)
        => new(account, risk, capital, fxLow, fxHigh, maxPerInstrument,
            [.. eligibleIds.Select(g => g.ToString()).Order(StringComparer.Ordinal)], k4Stride,
            [.. sampleKeys.Order(StringComparer.Ordinal)]);

    /// <summary>The names of the fields on which the two fingerprints differ (empty when equal); decimals compare by value.</summary>
    internal static IReadOnlyList<string> DiffFingerprints(TruthFingerprint expected, TruthFingerprint actual)
    {
        var diff = new List<string>();
        void Check(string name, bool same)
        {
            if (!same)
                diff.Add(name);
        }

        Check("account", expected.Account == actual.Account);
        Check("risk", expected.Risk == actual.Risk);
        Check("capital", expected.Capital == actual.Capital);
        Check("fxLow", expected.FxLow == actual.FxLow);
        Check("fxHigh", expected.FxHigh == actual.FxHigh);
        Check("maxPerInstrument", expected.MaxPerInstrument == actual.MaxPerInstrument);
        Check("eligibleStrategyIds", expected.EligibleStrategyIds.SequenceEqual(actual.EligibleStrategyIds));
        Check("k4Stride", expected.K4Stride == actual.K4Stride);
        Check("sampleKeys", expected.SampleKeys.SequenceEqual(actual.SampleKeys));
        return diff;
    }

    internal static string SerializeTruth(TruthFingerprint fingerprint, IReadOnlyList<FtmoGroupSearchRanking.RankEntry> entries)
        => System.Text.Json.JsonSerializer.Serialize(new TruthFile(TruthSchemaVersion, fingerprint, entries), TruthJson);

    /// <summary>
    /// Parses a truth file and checks it against <paramref name="expected"/>. Throws <see cref="InvalidOperationException"/>
    /// (never silently reuses) on unreadable content, an unknown schema version or any differing fingerprint field.
    /// </summary>
    internal static IReadOnlyList<FtmoGroupSearchRanking.RankEntry> DeserializeTruth(string json, TruthFingerprint expected)
    {
        TruthFile? file;
        try
        {
            file = System.Text.Json.JsonSerializer.Deserialize<TruthFile>(json, TruthJson);
        }
        catch (System.Text.Json.JsonException e)
        {
            throw new InvalidOperationException($"The truth file is unreadable ({e.Message}); delete it to regenerate the ground truth.", e);
        }

        if (file is null || file.Fingerprint is null || file.Entries is null)
            throw new InvalidOperationException("The truth file is unreadable (missing content); delete it to regenerate the ground truth.");

        if (file.SchemaVersion != TruthSchemaVersion)
            throw new InvalidOperationException(
                $"The truth file has schema version {file.SchemaVersion}, expected {TruthSchemaVersion}; delete it to regenerate the ground truth.");

        var diff = DiffFingerprints(expected, file.Fingerprint);
        if (diff.Count > 0)
            throw new InvalidOperationException(
                $"The truth file is stale: its fingerprint differs from this run in {string.Join(", ", diff)}. "
                + "Delete it (or point FTMO_CALIBRATION_TRUTH_FILE elsewhere) to regenerate the ground truth.");

        return file.Entries;
    }

    /// <summary>Writes <paramref name="content"/> to a temp file beside <paramref name="path"/>, then moves it over the target.</summary>
    internal static void WriteTruthAtomically(string path, string content)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    /// <summary>A candidate's identity: its member ids, sorted.</summary>
    internal static string Key(IEnumerable<Guid> memberIds) => string.Join(",", memberIds.Order());
}

/// <summary>
/// ftmo-group-search D9 — throws on any database command that is not a single read, so a calibration run against a
/// real database cannot write. An ALLOWLIST, not a denylist: comments are stripped and string literals and quoted
/// identifiers are masked by a literal-aware scanner, then the remaining code must start with <c>SELECT</c> or
/// <c>WITH</c>, hold no statement separator (one trailing <c>;</c> is allowed), and no <c>INTO</c>, <c>EXEC</c> or
/// <c>EXECUTE</c> keyword. Defence in depth with <see cref="NoSaveChangesInterceptor"/> and a no-tracking context.
/// </summary>
internal sealed class ReadOnlyCommandInterceptor : DbCommandInterceptor
{
    private static readonly Regex StartsWithRead = new(@"^(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Forbidden = new(@"\b(INTO|EXEC|EXECUTE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>True only for a single SELECT/WITH statement (see the type's summary). Anything unparsable is refused.</summary>
    internal static bool IsSelectOnly(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || Mask(sql) is not { } masked)
            return false;

        var code = masked.Trim();
        if (code.EndsWith(';'))
            code = code[..^1].TrimEnd();

        return !code.Contains(';') && StartsWithRead.IsMatch(code) && !Forbidden.IsMatch(code)
            && !TopLevelWrite.IsMatch(TopLevel(code));
    }

    /// <summary>A write as the statement a WITH clause introduces (e.g. <c>WITH x AS (...) DELETE ...</c>).</summary>
    private static readonly Regex TopLevelWrite = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The masked code outside every parenthesis: the main statement, with the CTE bodies and subqueries removed.</summary>
    private static string TopLevel(string code)
    {
        var top = new StringBuilder(code.Length);
        var depth = 0;
        foreach (var c in code)
        {
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (depth == 0) top.Append(c);
            else continue;

            if (c is '(' or ')') top.Append(' ');
        }

        return top.ToString();
    }

    /// <summary>
    /// The code with every comment replaced by a space and every string literal / quoted identifier by a neutral
    /// placeholder, or null when a literal, identifier or comment is unterminated. T-SQL block comments nest.
    /// </summary>
    private static string? Mask(string sql)
    {
        var code = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';
            if (c == '-' && next == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                    i++;
                code.Append(' ');
            }
            else if (c == '/' && next == '*')
            {
                var depth = 0;
                do
                {
                    if (i + 1 >= sql.Length)
                        return null;
                    if (sql[i] == '/' && sql[i + 1] == '*') { depth++; i += 2; }
                    else if (sql[i] == '*' && sql[i + 1] == '/') { depth--; i += 2; }
                    else i++;
                }
                while (depth > 0);
                code.Append(' ');
            }
            else if (c is '\'' or '"' or '[')
            {
                var close = c == '[' ? ']' : c;
                i++;
                while (true)
                {
                    if (i >= sql.Length)
                        return null;
                    if (sql[i] == close)
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == close) { i += 2; continue; }
                        i++;
                        break;
                    }

                    i++;
                }

                code.Append(" 0 ");
            }
            else
            {
                code.Append(c);
                i++;
            }
        }

        return code.ToString();
    }

    private static void Guard(DbCommand command)
    {
        if (IsSelectOnly(command.CommandText))
            return;

        var preview = command.CommandText.Length > 80 ? command.CommandText[..80] + "..." : command.CommandText;
        throw new InvalidOperationException($"Calibration is read-only: refused a command that is not a single SELECT/WITH read ({preview}).");
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Guard(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Guard(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Guard(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>ftmo-group-search D9 — a calibration context never saves.</summary>
internal sealed class NoSaveChangesInterceptor : SaveChangesInterceptor
{
    private static InvalidOperationException Refusal() => new("Calibration is read-only: SaveChanges is refused.");

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        => throw Refusal();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        => throw Refusal();
}
