---
name: job-orchestrator
description: Manage and schedule global background jobs (Cron, Stores) using the Scheduler features.
---

# 📅 Skill: Job Orchestrator

This skill enables the Agent to manage the **Global Job Scheduler** within the app-trading-algoritmico platform. It covers listing, updating, and triggering scheduled jobs like Shopify Order Sync or Inventory Sync.

## 🛠️ Capabilities

Use this skill when the user requests to:
- "Check what jobs are running"
- "Change the schedule of Order Sync to every 5 minutes"
- "Run the Inventory Sync now"
- "Add a new store to the Order Sync job"

## 🧠 Knowledge Base

### Data Model
- **Entity**: `ScheduledJob` (Domain/Entities)
- **Configuration**: `JobStoreConfig` (Many-to-Many link between Job and Stores)
- **Types**: `JobType` Enum (e.g., `ShopifyOrderSync`, `ShopifyInventorySync`)

### Infrastructure
- **Dispatcher**: `GlobalJobDispatcher` (Hangfire) - The central hub that executes the actual logic for each store.
- **Service**: `HangfireJobManager` - Wraps Hangfire's `RecurringJob` and `BackgroundJob`.

## 🕹️ key Actions

### 1. List Jobs
To see current schedules:
- **Code**: `GetScheduledJobsQuery`
- **API**: `GET /api/scheduler`
- **Result**: Returns list of jobs with Cron expressions and Active status.

### 2. Update Schedule
To change frequency or target stores:
- **Code**: `UpdateJobScheduleCommand`
- **API**: `PUT /api/scheduler/{id}`
- **Payload**:
  ```json
  {
    "id": "job-id",
    "cronExpression": "0 */5 * * *", // Every 5 minutes
    "shopifyStoreIds": ["store-id-1", "store-id-2"],
    "isActive": true
  }
  ```

### 3. Trigger Job Immediately
To force a run:
- **Code**: `TriggerJobCommand`
- **API**: `POST /api/scheduler/{id}/trigger`
- **Effect**: Enqueues a "Fire-and-Forget" job in Hangfire immediately.

## ⚠️ Critical Rules
1.  **Cron Validation**: Always validate the Cron expression. Use standard 5-part cron syntax.
2.  **Tenant Context**: Jobs run in the context of a Tenant. Ensure `IMustHaveTenant` is respected.
3.  **Store Context**: The `GlobalJobDispatcher` iterates stores. Ensure the Store ID is valid and belongs to the current Tenant.


