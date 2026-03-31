---
name: git-compare-branches
description: Compares two git branches to identify differences, potential conflicts, and recommend resolution strategies.
---

# 🕵️ Skill: Git Compare Branches

This skill allows the agent to analyze the differences between two git branches, detect potential merge conflicts, and provide a detailed report with recommended solutions.

## 📋 Usage
Trigger this skill when the user asks:
- "Compara la rama X con la rama Y"
- "What are the differences between main and dev?"
- "Will there be conflicts if I merge this?"
- "Help me resolve conflicts between branches."

## ⚙️ Process

### 1. 🔍 Identify Branches
Confirm the names of the source and target branches. If not specified, ask the user or assume `main` as target.

### 2. 📄 List Changed Files
Run the following command to see which files are different:
```powershell
git diff --name-status target_branch..source_branch
```
*Note: This shows what is in source that is not in target.*

### 3. 💣 Conflict Detection (Dry Run)
Identify potential conflicts by finding the merge base and simulating the merge:
```powershell
# Find common ancestor
$base = git merge-base target_branch source_branch
# Show changes that would be merged
git diff $base..source_branch
```
Alternatively, use `git merge-tree` to identify conflicts:
```powershell
git merge-tree --write-tree target_branch source_branch
```
*Check for "conflict" markers in the output.*

### 4. 🧠 Analysis & Reporting
- **New Features**: Identify new files or methods added.
- **Breaking Changes**: Look for modifications in shared interfaces or database schemas.
- **Conflicts**: For each file with a conflict, read the current version in both branches using `git show branch:path/to/file`.
- **Solution Proposal**: Compare the logic and suggest which version to keep or how to combine them (e.g., "Keep Target logic but add the new parameter from Source").

### 5. ✨ Refactoring/Conflict Resolution
If the user approves, proceed to:
1. `git checkout target_branch`
2. `git merge source_branch`
3. Resolve the conflicts in the files using `view_file` and `replace_file_content`.
4. Run tests to verify the merge.

## ⚠️ Standards & Rules
- **Safety**: Never perform a real merge without user confirmation.
- **Cleanliness**: Ensure the working directory is clean before attempting any merge simulation that modifies files.
- **Context**: Always consider the project context (Clean Architecture, Frontend Standards) when deciding "the best solution".
