# MeshBoard Server — Claude Project Instructions

## MCP Setup

Before any implementation session begins, ensure the Codex MCP server is registered:

```
claude mcp add codex -s user -- codex -m gpt-5.4 -c model_reasoning_effort="xhigh" mcp-server
```

This registers Codex as a user-scoped MCP tool available to all sessions in this project. It only needs to be run once per machine.

---

## Orchestration Model

Claude (this session) is the **orchestrator**. Codex (via MCP) is the **executor**.

Responsibilities:

| Role | Owned by |
|------|----------|
| Reading and interpreting the refactoring plan | Claude |
| Deciding which phase or slice to execute next | Claude |
| Writing the task prompt for each Codex invocation | Claude |
| Reviewing Codex output for correctness and plan alignment | Claude |
| Requesting fixes or follow-up slices | Claude |
| Generating, modifying, and compiling C# code | Codex |
| Running build verification after each slice | Codex |
| Producing commit-ready diffs | Codex |

Claude must never skip the review step. Every Codex output must be evaluated against the plan before the next slice is started.

---

## Source Of Truth

The authoritative implementation guide is:

```
docs/COLLECTOR_EVENT_DRIVEN_PLAN.md
```

Read this document at the start of every session. Do not rely on memory from a prior session. All architectural decisions, contracts, worker boundaries, phase ordering, and constraints are recorded there.

Supporting documents that must be respected:

- `docs/AGENT_CSHARP_STYLE.md` — C# style and layering conventions
- `docs/SDK_ASSEMBLY_CONSUMPTION_GUIDE.md` — rules for any worker that calls `MeshBoard.Api`
- `docs/PROJECT_FOUNDATIONS.md` — project-wide conventions
- `docs/COLLECTOR_POSTGRES_SCHEMA.md` — current PostgreSQL schema baseline

---

## Session Start Protocol

At the start of every implementation session:

1. Read `docs/COLLECTOR_EVENT_DRIVEN_PLAN.md` in full.
2. Check the current branch and recent git log to determine which phase was last completed.
3. Identify the next incomplete phase or open slice.
4. State the slice scope and acceptance criteria to the user before invoking Codex.
5. Wait for explicit user confirmation before starting execution.

---

## Codex Invocation Rules

When invoking Codex for a slice:

- Provide the full slice context in the prompt: phase name, goal, files to create or modify, contracts to respect, acceptance criteria.
- Include the relevant section of `COLLECTOR_EVENT_DRIVEN_PLAN.md` verbatim in the prompt so Codex has the authoritative spec without relying on retrieval.
- Specify the target project(s) and namespace(s) explicitly.
- Instruct Codex to follow the layering rules from `docs/AGENT_CSHARP_STYLE.md`: `Program.cs` bootstrap only, consumers transport-thin, services own orchestration, repositories own SQL.
- Instruct Codex to produce a build-clean result: no new compiler warnings, no `dotnet build` errors.
- Instruct Codex to stage output as a diff or set of file writes, not as narrative prose.

Codex prompt template:

```
You are executing one slice of the MeshBoard collector refactoring.

Plan reference:
<paste relevant plan section>

Slice goal:
<one sentence>

Files to create or modify:
<explicit list>

Constraints:
- Follow AGENT_CSHARP_STYLE.md layering rules
- No workspace or user identity in any collector type
- Consumers are transport adapters only — call services, do not contain logic
- SQL and repositories live in MeshBoard.Infrastructure.Persistence
- Contracts live in MeshBoard.Contracts
- Build must be clean after your changes

Acceptance criteria:
<paste from plan>

Produce file writes only. No prose explanation unless a constraint conflict requires it.
```

---

## Review Checklist After Each Codex Output

Before accepting a slice:

- [ ] New types respect the collector/product boundary (no workspace or user references in collector code)
- [ ] Consumers contain no business logic — they map and delegate only
- [ ] No SQL or repository instantiation in `Program.cs` or consumer constructors
- [ ] All new contracts are in `MeshBoard.Contracts`
- [ ] All new DI registrations are in extension classes under `DependencyInjection/`
- [ ] `dotnet build` passes with no errors or new warnings
- [ ] Acceptance criteria from the plan section are met
- [ ] The slice does not introduce work that belongs to a later phase

If any check fails, send a correction prompt to Codex with the specific failure before proceeding.

---

## Commit Protocol

After a slice passes review:

- Commit shape follows the plan's Git Guidance section.
- Branch name matches the workstream pattern: `refactor/collector-<workstream>`.
- Commit message uses conventional format: `feat:`, `refactor:`, `chore:` as appropriate.
- Do not squash mid-slice fixup commits before the slice is complete — keep history honest.
- Do not push until the user explicitly confirms.

---

## Phase Tracking

Phases are defined in `docs/COLLECTOR_EVENT_DRIVEN_PLAN.md`. Record completion here as phases land.

| Phase | Status | Branch | Notes |
|-------|--------|--------|-------|
| Phase 0: ADR and contracts freeze | pending | — | |
| Phase 1: Shared contracts and eventing infrastructure | pending | — | |
| Phase 2: Extract decode and channel resolution seams | pending | — | |
| Phase 3: Build collector ingress worker | pending | — | |
| Phase 4: Build collector normalizer worker | pending | — | |
| Phase 5: Build stats projector against current PostgreSQL model | pending | — | |
| Phase 6: Enable Timescale on collector PostgreSQL | pending | — | |
| Phase 7: Introduce optional hypertables and continuous aggregates | pending | — | |
| Phase 8: Build graph projector | pending | — | |
| Phase 9: Add Neo4j read seam | pending | — | |
| Phase 10: Topology analytics refinement | pending | — | |
| Retire MeshBoard.Collector | pending | — | Only after Phase 5 parity is proven |
