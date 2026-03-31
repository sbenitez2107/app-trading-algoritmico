---
name: perform-testing
description: Execute Unit, Integration, and E2E tests for both Host and Web projects.
---

# 🧪 Skill: Perform Testing

This skill orchestrates the execution of ensuring quality across the stack.

## 📋 Usage
Trigger this skill when the user asks to:
- "Run tests"
- "Test the application"
- "Check if it works"
- "Validate the module"
- "Levantar navegador para probar" (Launch browser to test)

## ⚙️ Process

### 1. Analysis
Determine the scope of the request. Does the user want automated unit tests, or a manual-like verification in the browser?

### 2. Frontend Testing (Web)

#### Automated Unit Tests
Use `npm test` to run the configured test runner (Vitest/Karma).
```powershell
cd app.trading.algoritmico.web
npm test -- --run # use --run to execute once and exit, if using Vitest
```

#### Browser Verification (E2E)
If the user asks to "lift the browser" or "test the module visually":
1. **Ensure App is Running**: Use `check-execution-status` to make sure Host and Web are up.
2. **Launch Browser Agent**: Use the `browser_subagent` tool.
   - **TaskName**: e.g., "Testing Shopify Integration Feature".
   - **Task**: Detailed instructions on what to click, what to type, and what to verify.
   - **RecordingName**: e.g., "shopify_feature_test".
3. **Review**: Analyze the screenshots or logs returned by the browser agent.

### 3. Backend Testing (Host)

#### Automated Tests
Run the .NET test suite.
```powershell
cd app.trading.algoritmico.api
dotnet test
```

## ⚠️ Standards & Rules
- **Non-Interactive**: When running unit tests, ensure they are in "watch=false" mode (CI mode) if possible, so they don't block the terminal.
- **Reporting**: Always report back to the user with a summary of Pass/Fail counts.
- **Screenshots**: When using the browser agent, always request screenshots of critical states (Success vs Error).


