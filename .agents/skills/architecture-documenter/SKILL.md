---
name: architecture-documenter
description: Manages architectural documentation, ADRs, and system blueprints to keep project documentation in sync with code.
---

# 🏗️ Skill: Architecture Documenter

This skill ensures that every significant change in the project's architecture or functionality is properly recorded in the system's technical documentation.

## 🎯 Trigger
Activate this skill when:
- Starting a new feature (to plan documentation).
- Changing an architectural pattern (e.g., switching from Services to UseCases).
- Adding a new integration (e.g., Shopify, ERP).
- Finishing a feature (to ensure the "Blueprint" and "Functionality" docs are updated).

## 🛠️ Protocols

### 1. ADR (Architecture Decision Record)
If a decision changes "how" things are built (e.g., "Use Signals for state management" or "Implement Outbound connectors using a specific interface"), create an ADR.

**Path**: `documentation/adr/ADR-XXX-{name}.md`
**Template**: (Use the template from `universal-skills/documentation-standard/skill.md`)

### 2. System Blueprint Update
The "System Blueprint" is a high-level overview of the current modules and their interactions.

**Path**: `documentation/architecture/SYSTEM_BLUEPRINT.md`
**Action**: Update the Mermaid diagrams and module lists after adding new core services or infrastructure components.

### 3. Feature Documentation
Ensure every new vertical feature has its own file in `documentation/functionality/`.

**Path**: `documentation/functionality/{feature-name}.md`

## 📝 Process Execution

1. **Review**: Identify what changed in the architecture or domain logic.
2. **Document**:
   - If it's a "why" (Decision) -> Create **ADR**.
   - If it's a "how it's built" (Technical Detail) -> Update/Create **Functionality Doc**.
   - If it's a "where it fits" (System Map) -> Update **System Blueprint**.
3. **Verify**: Ensure the documentation maps 1:1 with the implemented code.

## ⚠️ Standards
- **Sync**: Documentation MUST be updated in the same PR/Task as the code.
- **Visuals**: Use Mermaid diagrams for flows and relationships. Use **Sequence Diagrams** for any multi-step data processing or cross-system integration.
- **Terminology**: Use Domain-Driven Design (DDD) terms (Tenant, SKU, Context, etc.).
