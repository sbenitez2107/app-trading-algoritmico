---
name: documentation-features
description: Standard procedure for documenting full-stack features to facilitate replication and maintenance.
---

# Feature Documentation Skill

This skill guides the creation of comprehensive documentation for project features, ensuring consistency and ease of replication across projects.

## 1. Objective
To generate a detailed technical breakdown of a specific feature, covering all layers (Database, Backend, Frontend) and providing a "Recipe" for porting the feature to another codebase.

## 2. Output Location
All feature documentation must be saved in:
`[ProjectRoot]/documentation/features/[feature-name-kebab-case].md`

## 3. Documentation Standard Template

Use the following Markdown structure for every feature document:

```markdown
# [Feature Name] Feature Documentation

## 1. Overview
[Brief description of what the feature does, its business value, and key capabilities.]

## 2. Architecture & Patterns
- **Frontend**: [Tech stack, e.g., Angular, Signals, specialized libraries]
- **Backend**: [Tech stack, e.g., .NET 8, CQRS, special middleware]
- **Database**: [DB type, key conceptual relationships]

## 3. Backend Implementation

### 3.1. Domain Layer
**Entity**: `[EntityName]`
- **Location**: `[Path]`
- **Key Properties**:
  - `[Prop1]`: [Description]
  - ...

### 3.2. Application Layer (CQRS)
**Location**: `[Path]`

#### Commands:
- `[CommandName]`: [Purpose]

#### Queries:
- `[QueryName]`: [Purpose]

### 3.3. API Layer
**Controller**: `[ControllerName]`
- **Location**: `[Path]`
- **Endpoints**:
  - `[METHOD] /route/path`: [Description]

## 4. Frontend Implementation

### 4.1. Components
**[Component Name]**: `[ClassName]`
- **Location**: `[Path]`
- **Features**: [List of UI features/behaviors]

### 4.2. Services
**Service**: `[ServiceName]`
- **Location**: `[Path]`

### 4.3. Models & Store
- **Models**: `[Path]`
- **State Management**: [Description if NGRX/Signals/etc is used]

## 5. Database & Migrations
- **Tables**: `[Table1]`, `[Table2]`
- **Migrations**: [Relevant migration names or IDs]

## 6. Replication Steps (How to port)
[Step-by-step guide to copy-paste and adapt this feature to a new solution]
1.  Copy Domain Entity...
2.  Scaffold CQRS...
3.  ...
```

## 4. Execution Steps

1.  **Identify Scope**: Determine the boundaries of the feature (e.g., "Job Scheduler" vs "Entire System").
2.  **Scan Codebase**:
    *   Find Domain Entities.
    *   Find related Controllers and CQRS handlers.
    *   Find Angular Components, Routes, and Services.
3.  **Draft Content**: Fill out the template above with precise paths and names found in step 2.
4.  **Review**: Ensure "Replication Steps" are actionable and clear.
5.  **Save**: Write the file to the target directory.
