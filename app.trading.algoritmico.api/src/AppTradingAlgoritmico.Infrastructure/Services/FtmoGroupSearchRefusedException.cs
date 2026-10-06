namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D7 - a search the runner refuses to start (an unusable request, no limits row, a pool above
/// <see cref="FtmoGroupSearchLimits.MaxPoolSize"/>). The message is authored by the runner, carries no internals, and the
/// worker shows it to the user as the failed job message.
/// </summary>
internal sealed class FtmoGroupSearchRefusedException(string message) : InvalidOperationException(message);
