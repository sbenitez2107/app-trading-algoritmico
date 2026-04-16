---
name: trading-domain
description: Routes agents to the correct IMOX knowledge base documents before making any domain-related decision about trading strategies, pipeline stages, SQX configuration, or selection criteria.
---

# Trading Domain Knowledge

This skill ensures that agents consult the IMOX Academy knowledge base before making domain decisions. It acts as a **knowledge router** — not a knowledge source itself.

---

## Trigger

Activate this skill whenever the task involves:

- Analyzing requirements for features related to the strategy pipeline (Builder → Retester → Optimizer → Demo → Live)
- Proposing or modifying Analyzer Rules
- Designing per-stage configuration, date tracking, or stage transitions
- Evaluating KPIs, performance metrics, or acceptance thresholds
- Making decisions about Demo/Live deployment of strategies
- Any task where trading domain terminology is used (drawdown, Sharpe ratio, building blocks, optimization, walk-forward, etc.)

---

## Process

### Step 1 — Read the index
Always start here:
```
.agents/knowledge/imox/INDEX.md
```

### Step 2 — Identify relevant documents
Use the **Trigger Table** in INDEX.md to identify which documents apply to the current task.

### Step 3 — Read only the relevant documents
Read the minimum number of documents needed to inform the decision. Do not read all documents.

### Step 4 — Apply the knowledge
Use the domain knowledge to:
- Validate that requirements align with IMOX methodology
- Propose implementations that respect stage selection criteria
- Use correct domain terminology in code, comments, and documentation

---

## Non-Negotiable Rules

- **NEVER invent domain criteria** — if selection criteria, KPI thresholds, or SQX parameters are needed and not found in the knowledge base, flag it to the user and ask before proceeding
- **Knowledge base is authoritative** — if the codebase contradicts the knowledge base, flag the discrepancy to the user
- **Index first, documents second** — always read INDEX.md before opening any knowledge document
