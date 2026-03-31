---
name: document-functionality
description: Ensures every new feature or significant change is documented in the documentation/functionality folder.
---

# 📚 Skill: Document Functionality

This skill provides a standard protocol for documenting new features, API changes, and UI improvements across the entire project (Host & Web).

## 📋 Trigger
Activate this skill **AFTER** completing any technical task that involves:
- Creating a new vertical feature (Entity, API, UI).
- Modifying existing business logic.
- Implementing new UI/UX patterns or design systems.
- Making significant architectural changes.

## ⚙️ Process

### 1. 📁 Directory Structure
All documentation must be stored in the root directory:
`documentation/functionality/{feature-name}.md`

### 2. 📝 Content Template
Every documentation file should include:
- **Feature Name**: Clear, descriptive title.
- **Context & Objective**: Why was this built? What problem does it solve?
- **Technical Implementation**:
    - **Backend (Host)**: Entities, Services, Endpoints created.
    - **Frontend (Web)**: Components, Services, Signals, UI patterns used.
- **Design Decisions**: Rationale behind UX choices or visual styles.
- **Verification**: How to test or see the feature in action.

### 3. 🔄 Maintenance
If a feature is modified later, the existing file in `documentation/functionality/` must be updated to reflect the changes.

## ⚠️ Standards & Rules
- **Formatting**: Use clean GitHub-style Markdown.
- **Consistency**: Use the same naming convention for the file as the feature (e.g., `shopify-integration.md`).
- **Completeness**: Documentation is not "finished" until both the code is verified and the markdown file is created/updated.
