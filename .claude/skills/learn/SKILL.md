---
name: learn
description: Reflect on recent work and propose organic updates to .claude/ — new rules, skills, CLAUDE.md knowledge, or memory entries that would make future sessions more effective
user-invokable: true
---

You are a **learning facilitator** for the SBESharp project. Your job is to make the `.claude/` directory smarter and more useful over time by extracting knowledge from recent work and proposing concrete additions.

Think of this as a retrospective: what did we just learn that future-Claude should already know?

---

## Steps

### 1. Gather context

Run these in parallel to build a picture of recent work:

- `git log --oneline -20` — what was worked on recently
- `git diff HEAD~5 HEAD --stat` — which files changed most
- `git log --oneline --all --since="7 days ago"` — recent activity window
- Read `.claude/CLAUDE.md` — what's already documented
- Read `.claude/rules/code-style.md`, `performance.md`, `testing.md` — existing rules
- List `.claude/skills/` — existing skills
- Read the auto-memory index at `~/.claude/projects/-Users-samuelgaudreau-dev-SBESharp/memory/MEMORY.md`

### 2. Identify learning opportunities

Look for patterns across the recent work and ask yourself:

**For CLAUDE.md** (architecture, vision, business knowledge):
- Was a non-obvious design decision made that should be documented as a project constraint?
- Was a new concept introduced (new IR node, new phase, new abstraction) that isn't in CLAUDE.md yet?
- Did the project scope or vision evolve in a meaningful way?
- Is there domain knowledge (SBE spec behavior, wire format edge cases) that isn't written down?

**For `.claude/rules/`** (hard-won lessons about code quality):
- Did we encounter a compiler/analyzer error more than once that a rule would prevent?
- Did a code review surface a pattern that should be enforced going forward?
- Was a performance constraint discovered that isn't captured in `performance.md`?
- Did a test failure reveal an assumption that should be a rule?

**For `.claude/skills/`** (new automatable workflows):
- Is there a recurring task (e.g., "add a new IR node + emitter + test", "validate a new .sbe.xml schema", "add a new primitive type") that could be a skill?
- Is there a quality gate or checklist that would benefit from a dedicated `/skill`?
- Did the user suggest or imply they'd want a slash command for something?

**For memory** (cross-session context in `~/.claude/projects/.../memory/`):
- Was a non-obvious project decision made that future sessions should know about?
- Did the user express a strong preference, workflow habit, or "never do X" that should be saved?
- Did we learn something about the build system, tooling, or CI that isn't in CLAUDE.md?

### 3. Generate proposals

For each learning opportunity you find, produce a **concrete proposal** in this format:

---
**Proposal: [short title]**
**Target:** `[file path]` (e.g., `.claude/CLAUDE.md`, `.claude/rules/testing.md`, `.claude/skills/add-ir-node/SKILL.md`, `memory/feedback_xyz.md`)
**Type:** `[CLAUDE.md addition | new rule | rule update | new skill | memory update]`
**Trigger:** What recent work surfaced this? (cite commit hash or file)

**Proposed content:**
```
[exact text to add/create — ready to paste]
```
---

Only surface things that are genuinely new and not already captured. Aim for quality over quantity — 2–5 high-signal proposals is better than 10 noisy ones.

### 4. Confirm and apply

Present all proposals to the user as a numbered list with a brief rationale for each.

Ask: **"Which of these should I apply? (e.g., 'all', '1 3', or 'none')"**

Wait for the user's response before writing anything.

For each approved proposal:
- Write or edit the target file with the proposed content
- Update `MEMORY.md` if a new memory file was created
- Confirm each change with the file path written

---

## Tone

Be direct and specific. This is not a brainstorm — proposals should be actionable and precise. If you're not sure whether something is worth capturing, default to proposing it and let the user decide.
