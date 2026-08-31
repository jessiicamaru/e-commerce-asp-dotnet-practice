---
name: compact
description: Advanced automated documentation and project state tracker. Features branch-aware state management in a single unified state file, token-optimized diffing (via --stat and logs), and architecture-aware extraction.
when_to_use: "When the user types `/compact <path>`, requests a project update, or after significant refactoring, branch switching, or architecture changes."
allowed-tools: Read, Write, Glob, Git_Execute (or Terminal/Bash)
---

# SKILL: /compact

## OBJECTIVE

Maintain an up-to-date project walkthrough document strictly at `/docs/project-walkthrough/walkthrough.md`. You must track progress, architecture decisions, and code changes while strictly managing token limits and handling Git branch context switches gracefully.
**STRICT RULE:** All context and state files MUST remain within `docs/project-walkthrough/`. Under no circumstances are you allowed to create or modify walkthrough files or state files outside of this specific directory.

## EXECUTION WORKFLOW

### Step 1: Automated Context Gathering (The Runner)

Instead of running individual Git commands, simply execute the companion script:

1. Run `python .agent/skills/compact/scripts/compact_runner.py`
2. Read the output file `compact_context.json`. This file automatically contains:
   - Current branch and commits
   - Formatted `git log` and `git diff --stat` (if updating)
   - Extracted TODOs and FIXMEs
   - It also automatically respects your project's `.gitignore` rules.
3. Check if `/docs/project-walkthrough/walkthrough.md` exists. If yes, create a backup: `/docs/project-walkthrough/walkthrough.md.bak` if you are about to do a major overwrite.

### Step 2: Smart Analysis

Based on the `mode` in `compact_context.json`:

#### Path A: INITIALIZATION (`mode: "INITIALIZATION"`)

1. Read core configs (`package.json`, `pubspec.yaml`, etc.) for dependencies.
2. Perform **Architecture-Aware Analysis**: Scan for specific design patterns and note them in the walkthrough:
   - **Domain-Driven Design (DDD):** Identify Bounded Contexts, Entities, and Value Objects.
   - **Hexagonal Architecture:** Locate Ports (interfaces) and Adapters (implementations).
   - **Microservices & Distributed Systems:** Look for message brokers and distributed transaction implementations.
   - **Machine Learning Pipelines:** Identify specific algorithm variants in use.

#### Path B: UPDATE (`mode: "UPDATE"`)

1. Review `git_log` and `git_diff_stat` in the JSON to understand the _intent_ and _scope_ of the changes.
2. If `git_diff` is provided in the JSON, use it to analyze logical code changes. (The script automatically omits this if the diff is massive).

### Step 3: Document Generation

Update `/docs/project-walkthrough/walkthrough.md` using the strict Markdown template.

- Integrate the `git log` messages into the "Changelog" section to explain _why_ changes occurred.
- Group new features by their Bounded Context or Microservice domain if applicable.

### Step 4: State Preservation

Overwrite the **single unified** `docs/project-walkthrough/.walkthrough_state.json` with a dictionary keyed by branch name:
```json
{
  "main": {
    "last_updated": "[Current Timestamp]",
    "branch": "main",
    "last_commit_id": "<latest_HEAD_commit_id>"
  },
  "feature/some-branch": {
    "last_updated": "[Timestamp]",
    "branch": "feature/some-branch",
    "last_commit_id": "<commit_id>"
  }
}
```

**IMPORTANT:** Never create per-branch files like `.walkthrough_state_main.json`. Always write to the single `.walkthrough_state.json`.
The script will **auto-migrate** any old per-branch files it finds by absorbing them into the unified file and deleting them.

---

## MARKDOWN TEMPLATE (`/docs/project-walkthrough/walkthrough.md`)

# 🚀 Project Walkthrough

> **Note for AI:** State tracked via branch-specific JSON. Do not modify this header block.
> **Current Branch:** `[Branch Name]` | **Last Analyzed Commit:** `[Commit ID]`

## 🎯 Vision & Business Logic

[High-level summary of the system and its primary use case]

## 🏗 System Architecture & Core Stack

- **Tech Stack:** [Languages, Frameworks]
- **Architectural Patterns:** [e.g., DDD, Hexagonal, Saga orchestration]
- **Data & ML:** [Databases, specific models like MultinomialNB, etc.]

## ✨ Modules & Feature Tracking

### [Bounded Context / Microservice Name 1]

- [ ] **[Feature]:** [Description]
- [x] **[Feature]:** [Description]

### [Bounded Context / Microservice Name 2]

- [ ] **[Feature]:** [Description]

## 📝 Changelog & Developer Intent

_Generated from git logs and structural diffs._

- **[YYYY-MM-DD] - [Commit Hash]: [Commit Message]**
  - _Details:_ [AI analysis of the diff, e.g., "Updated Saga orchestrator to handle payment rollback events", "Modified classification pipeline to correctly apply MultinomialNB parameters"].

## 📌 Action Items

- [ ] `[File:Line]`: [Extracted TODO/FIXME]
