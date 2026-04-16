# Backend External — Refit, Polly & Third-Party APIs

Consolidated from: `external-integrations` skill.
Applies to: `Application/Interfaces/I*Client.cs`, `Infrastructure/DependencyInjection.cs`.

---

## Refit Interface (Application layer)

```csharp
public interface IBrokerApiClient
{
    [Get("/api/users/{id}")]
    Task<ExternalUserDto> GetUserAsync(string id);

    [Post("/api/users")]
    Task<ExternalUserDto> CreateUserAsync([Body] CreateExternalUserDto dto);

    [Get("/api/products")]
    Task<IEnumerable<ExternalProductDto>> GetProductsAsync(
        [Query] string? category = null, [Query] int? limit = null);
}
```

## Registration with Polly (Infrastructure layer)

```csharp
// Retry: 3 attempts, exponential backoff (2^attempt seconds)
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

// Circuit breaker: 5 failures → open for 30s
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

services.AddRefitClient<IBrokerApiClient>()
    .ConfigureHttpClient(c => { c.BaseAddress = new Uri(baseUrl); c.Timeout = TimeSpan.FromSeconds(30); })
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
```

## AuthHeaderHandler — propagate API key

```csharp
public class AuthHeaderHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var apiKey = _configuration["ExternalServices:BrokerApi:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey)) request.Headers.Add("X-API-Key", apiKey);
        return await base.SendAsync(request, ct);
    }
}
```

## External DTOs — separate folder

DTOs in `Application/DTOs/External/` with `[JsonPropertyName]` for snake_case APIs.

## Error Handling

```csharp
try { return await _client.GetUserAsync(id); }
catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
catch (ApiException ex) { _logger.LogError(ex, "Error for {UserId}", id); throw; }
```

## Configuration

```json
{ "ExternalServices": { "BrokerApi": { "BaseUrl": "https://...", "Timeout": 30 } } }
```

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| Direct HttpClient | Use Refit |
| Infinite retry | Max 3-5 retries |
| No circuit breaker | Add Polly |
| No timeout | Configure per client |
