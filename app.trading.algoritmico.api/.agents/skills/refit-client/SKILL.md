---
name: Implement Refit Client
description: Mandatory standard for implementing communication with external APIs (Shopify, ERPs, Logistics) using Refit.
---

# 🦸 Skill: Implement Refit Client

This skill ensures that all external API integrations follow the **The Golden Stack** standard using **Refit** for declarative REST clients, ensuring consistency, resilience, and multi-tenancy support.

## 📋 Usage
Use this skill whenever you need to:
- Create a new integration with an external service (Shopify, Acumatica, Everest, etc.).
- Add new endpoints to an existing external client.
- Refactor legacy `HttpClient` calls to the declared standard.

## ⚙️ Process

### 1. Define the Interface
Create or update the interface in `AppTradingAlgoritmico.Application/Interfaces/`.
Use Refit attributes for methods and headers.

```csharp
using Refit;

public interface IShopifyApiClient
{
    [Get("/admin/api/{apiVersion}/orders.json")]
    Task<ShopifyOrderResponse> GetOrdersAsync(string apiVersion, [Header("X-Shopify-Access-Token")] string accessToken);
}
```

### 2. Define Data Transfer Objects (DTOs)
Create DTOs in a specific folder within `Application/Features/[FeatureName]/DTOs` or a shared `Integration` namespace if shared.

### 3. Register in Infrastructure
Update `AppTradingAlgoritmico.Infrastructure/DependencyInjection.cs` inside the `ConfigureRefitClients` method.

**Mandatory Patterns**:
- **BaseAddress**: Use configuration keys (`ExternalServices:[ServiceName]:BaseUrl`).
- **Resilience**: Attach Polly retry and circuit breaker policies.
- **Multi-tenancy**: Always add `.AddHttpMessageHandler<TenantHeaderHandler>()` if the external service requires a Tenant context.

```csharp
services.AddRefitClient<IShopifyApiClient>()
    .ConfigureHttpClient(c => {
        c.BaseAddress = new Uri(configuration["ExternalServices:Shopify:BaseUrl"]);
    })
    .AddHttpMessageHandler<TenantHeaderHandler>()
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
```

### 4. Inject and Use
Inject the interface into your Application Services or Job Handlers.

## ⚠️ Standards & Rules
- **NEVER** use `HttpClient` directly for external APIs unless Refit is technically impossible.
- **Headers**: Use `[Header]` for static headers and `[AliasAs]` for dynamic parameters.
- **Error Handling**: Refit throws `ApiException`. Always wrap calls in try-catch or use a middleware to handle external failures gracefully.
- **Naming**: Interfaces must end with `Client` (e.g., `IEverestClient`).


