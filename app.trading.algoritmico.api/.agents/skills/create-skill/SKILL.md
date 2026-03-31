---
name: Create Skill
description: Guides the creation of new Agent Skills to extend the AI's capabilities.
---

# 🧠 Skill: Create Skill

This skill guides the process of defining new capabilities (Skills) for the AI agent for this project.

## 📋 usage
Use this when the user says "create a new skill called [Name]" or "teach you how to [Task]".

## ⚙️ Process

### 1. 📁 Create Directory
Determine a kebab-case name for the skill (e.g., `deploy-aws`, `manage-users`).
Create the directory:
```powershell
mkdir .agents/skills/[skill-name]
```

### 2. 📝 Create SKILL.md
Create the file `.agents/skills/[skill-name]/SKILL.md`.
**Template**:

```markdown
---
name: [Human Readable Name]
description: [Short description of what this skill does]
---

# 🦸 Skill: [Human Readable Name]

[Brief introduction: What acts does this skill cover?]

## 📋 Usage
[When should the agent use this? Specific triggers or keywords.]

## ⚙️ Process / Steps
[Step-by-step instructions. Be extremely specific.]
[Include code snippets, exact file paths, or command templates.]

### 1. Step One
...

### 2. Step Two
...

## ⚠️ Standards & Rules
[Crucial "Dos and Don'ts" for this specific task.]
```

### 3. 🧪 Validation (Mental Check)
Before finishing, ask yourself:
- Is the prompt clear?
- Are the CLI commands safe?
- Does it adhere to the project's `Agents.md` rules?

### 4. 📢 Announcement
Inform the user the skill is ready: "I have learned the skill [Name]. You can now ask me to..."
