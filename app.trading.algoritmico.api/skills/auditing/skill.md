---
name: auditing
description: >
  Decision rules for the intelligent auditing system in app-trading-algoritmico.
  Defines filtering, masking, and truncation of HTTP audit logs.
  Trigger: When implementing or modifying API request auditing logic.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
  standard: ASP.NET Core Middleware
---

## When to Use

Use this skill when:
- Implementing HTTP auditing middleware
- Modifying the `AuditLog` entity
- Configuring which requests to audit
- Handling sensitive data in logs

---

## Decision Rules (Mandatory)

### Rule 1: Filtering (Optimization)

The agent MUST skip creating audit records for **GET** requests.

**Justification**: GET operations are read-only and do not alter system state.

```csharp
// ✅ AUDIT
POST, PUT, PATCH, DELETE

// ❌ DO NOT AUDIT
GET, HEAD, OPTIONS
```

**Implementation**:
```csharp
if (HttpMethods.IsGet(context.Request.Method) ||
    HttpMethods.IsHead(context.Request.Method) ||
    HttpMethods.IsOptions(context.Request.Method))
{
    await _next(context);
    return; // Skip audit
}
```

---

### Rule 2: Masking (Privacy)

In the `Parameters` and `ReturnValue` fields, the agent MUST find and replace sensitive key values with `***`.

**Sensitive Keys**:
| Key Pattern | Example |
|-------------|---------|
| `password` | `"password": "***"` |
| `token` | `"token": "***"` |
| `secret` | `"clientSecret": "***"` |
| `apiKey` | `"apiKey": "***"` |
| `authorization` | `"authorization": "***"` |
| `credentials` | `"credentials": "***"` |

**Implementation**:
```csharp
private static readonly string[] SensitiveKeys = 
{
    "password", "token", "secret", "apikey", 
    "authorization", "credentials", "key"
};

public static string MaskSensitiveData(this string json)
{
    if (string.IsNullOrEmpty(json)) return json;
    
    foreach (var key in SensitiveKeys)
    {
        // Regex pattern: "key": "value" → "key": "***"
        var pattern = $@"(""{key}""\s*:\s*)""[^""]*""";
        json = Regex.Replace(json, pattern, "$1\"***\"", 
            RegexOptions.IgnoreCase);
    }
    return json;
}
```

---

### Rule 3: Truncation (Storage Limit)

Apply the ABP standard of limiting payloads to **1024 characters** using `TruncateWithPostfix`.

**Justification**: Prevent database overflow errors and optimize storage.

**Implementation**:
```csharp
public static string TruncateWithPostfix(this string? value, int maxLength, string postfix = "...")
{
    if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        return value ?? string.Empty;
    
    return value.Substring(0, maxLength - postfix.Length) + postfix;
}

// Usage
auditLog.Parameters = requestBody.MaskSensitiveData().TruncateWithPostfix(1024);
auditLog.ReturnValue = responseBody.MaskSensitiveData().TruncateWithPostfix(1024);
```

---

## AuditLog Entity (ABP Standard)

The entity must include the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | string? | User who performed the action |
| `ServiceName` | string | Controller name |
| `MethodName` | string | Action name |
| `Parameters` | string? | Request body (masked, truncated) |
| `ReturnValue` | string? | Response body (masked, truncated) |
| `ExecutionDuration` | int | Duration in milliseconds |
| `ClientIpAddress` | string? | Client IP address |
| `BrowserInfo` | string? | User-Agent |
| `HttpMethod` | string | GET, POST, PUT, DELETE |
| `Url` | string | Full request URL |
| `HttpStatusCode` | int | HTTP response code |
| `ExceptionMessage` | string? | Exception message if error occurred |
| `AuditDate` | DateTime | Audit timestamp (UTC) |

---

## Middleware Pattern

```csharp
public class AuditLogMiddleware
{
    public async Task InvokeAsync(HttpContext context, ...)
    {
        // 1. Skip GET requests (Rule 1)
        if (IsReadOnlyMethod(context.Request.Method))
        {
            await _next(context);
            return;
        }
        
        // 2. Capture request body
        var requestBody = await ReadRequestBody(context);
        
        // 3. Start timer
        var stopwatch = Stopwatch.StartNew();
        
        // 4. Execute pipeline
        await _next(context);
        
        // 5. Stop timer
        stopwatch.Stop();
        
        // 6. Capture response body
        var responseBody = await ReadResponseBody(context);
        
        // 7. Create audit log (async, fire-and-forget)
        _ = Task.Run(() => SaveAuditLogAsync(
            context, 
            requestBody.MaskSensitiveData().TruncateWithPostfix(1024),
            responseBody.MaskSensitiveData().TruncateWithPostfix(1024),
            stopwatch.ElapsedMilliseconds
        ));
    }
}
```

---

## Anti-Patterns

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Auditing GETs** | Excessive log volume | Filter by Rule 1 |
| **Storing passwords in plain text** | Security vulnerability | Apply Rule 2 |
| **Unlimited payloads** | DB overflow | Apply Rule 3 |
| **Synchronous persistence** | Blocks API response | Use fire-and-forget |
