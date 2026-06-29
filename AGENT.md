# vantage AI Agent Guide

Project name: vantage
Workflow guide version: v6.2
Project type: Godot 4.7 .NET C# top-down multiplayer shooter

This file is the authoritative local workflow guide for AI-assisted passes in this repository. Follow it before any pass-specific prompt unless the user explicitly supersedes it.

## Stable Project Facts

- Architecture decision: Path B - Godot .NET client + Godot .NET headless authoritative server + Nakama for auth, matchmaking, and non-gameplay services.
- Shared protocol/library plan: `Vantage.Shared` plain .NET library will be created later. Protocol DTOs and shared gameplay math must live there once P02+ reaches that scope.
- Networking plan: server authoritative, 60 Hz Godot physics tick, client sends inputs, server sends authoritative snapshots, no Nakama gameplay simulation.
- Serialization decision: MemoryPack is preferred for protocol DTOs unless a later explicit architecture pass changes it.
- Nakama client decision: use `nakama-dotnet` for the Godot desktop C# client. Do not introduce a GDScript Nakama SDK split.
- P01 state: no Godot projects yet, no solution yet, no C# projects yet.
- P02 should create the empty Godot/.NET project skeleton.

## Default Constraints

- Default: no tool remediation unless `AI_ALLOW_TOOL_REMEDIATION=1`.
- Homebrew operations remain disallowed unless separately authorized.
- Do not commit, push, tag, publish, deploy, or release unless explicitly asked. Exception: P01 bootstrap and `AGENT.md` maintenance passes require a local commit.
- Do not read, print, copy, or transmit secret files or credentials.
- Treat as sensitive by path only: `.env`, `.env.*`, files matching `*secret*`, `*token*`, `*credential*`, SSH private keys, signing certificates, provisioning profiles, notarization credentials, keychains.
- Do not modify `.git/**`, `.github/workflows/**`, dependency lockfiles unless in scope and documented, signing/notarization files, entitlements, production credentials/env files, user data, unrelated assets.
- Do not modify `AGENT.md` or `metadata-schema.json` during normal passes.
- Preserve existing working behavior unless a pass explicitly changes it.

## Protected Paths

- `build-report-logs/**`
- `server/nakama/data/**`
- `.env`, `.env.*`, `*secret*`, `*token*`, `*credential*`
- `.git/**`
- `.github/workflows/**` unless a pass explicitly scopes CI
- `client/**` and `server/headless/**` during P01 except `.gitkeep` placeholders if needed

`build-report-logs/` is local-only and must remain gitignored. No gameplay, Godot project, solution, C# code, CI workflow, Nakama module, or asset work is allowed in P01.

## Stop Conditions

Stop and ask the user before continuing if any of these occur:

- A requested change requires reading or exposing sensitive files or credentials.
- The working tree has unrelated dirty changes that overlap files required by the pass.
- A pass requires Homebrew, package-manager remediation, deploy, publish, push, release, or credential changes without explicit authorization.
- Required project facts conflict with existing repository files.
- Verification cannot be made honest without changing scope.

## Discovery Commands

Run these before modifying files unless the pass explicitly says otherwise:

```zsh
pwd
git status --short
git log --oneline -5
find . -maxdepth 4 -type f -not -path '*/.git/*' -not -path '*/build-report-logs/*' | sort
```

Then inspect relevant existing files before editing. Prefer `rg` for searching and `rg --files` for file listing when practical.

## Workflow State

Every pass report must include this table, updated honestly:

| Item | Status |
|---|---|
| Workflow guide version | v6.2 |
| AGENT.md present at project root | yes/no |
| AGENT.md authorized | yes/no |
| metadata-schema.json present at project root | yes/no |
| build-report-logs/ in .gitignore | yes/no |
| Prompt saved for this pass | yes/no |
| Launcher saved for this pass | yes/no |
| Prompt template for next pass | lean (§9) or full |
| Bootstrap pass needed | yes/no |
| AGENT.md maintenance score | 1-5 |

## Maintenance Score Rules

Use the `AGENT.md maintenance score` to decide whether guide maintenance is needed:

- 1: current and sufficient; no maintenance needed.
- 2: minor drift or small clarifications useful; maintenance optional.
- 3: noticeable drift; maintenance should be scheduled soon.
- 4: major drift; maintenance strongly recommended before more implementation.
- 5: blocking drift or incorrect authority; maintenance pass required before the next implementation pass.

Normal implementation passes must not edit `AGENT.md` or `metadata-schema.json`.

## Reports

Each pass must produce a markdown report under:

`build-report-logs/reports/<PASS_ID>-report.md`

Required report sections:

- `## Model Used`
- `## Workflow State`
- `## Files Read`
- `## Files Created`
- `## Files Modified`
- `## Implementation Details`
- `## Architectural Constraints Preserved`
- `## Verification Performed`
- `## Build Results`
- `## Artifacts`
- `## Manual Verification Checklist`
- `## Issues Encountered`
- `## Unresolved Issues`
- `## Recommended Next Prompt`

Reports must include the full pass id, date, status, workflow guide version, meaningful verification results, and any skipped checks.

## Metadata

Each pass must produce valid JSON metadata under:

`build-report-logs/metadata-reports/<PASS_ID>-metadata.json`

Metadata must validate against `metadata-schema.json` when present. Required truthfulness rules:

- `status` must match the work actually performed.
- `build_result` must be `null` for `DOCUMENTATION_ONLY` and `WORKFLOW_ONLY` passes.
- `duration_seconds` and `total_tokens_used` may remain `null` before post-pass enrichment.
- `committed` and `pushed` must reflect what happened.
- `files_read`, `files_created`, `files_modified`, and `pre_existing_dirty_files` must be honest.

## Verification Rules

- Run the pass-specific verification commands.
- Run `git status --short` and `git diff --stat` before committing or finishing.
- For documentation/workflow-only passes, do not manufacture build results.
- For code passes, run the narrowest meaningful build/test checks first, then broader checks when risk justifies it.
- Confirm no protected paths or unrelated files were staged before committing.
- Do not push unless explicitly asked.

## P01 Scope Lock

P01 is workflow-only adoption bootstrap. It may create workflow docs, reports, scripts, placeholders, `.gitignore` entries, and `global.json`. It must not create gameplay code, Godot projects, C# projects, solution files, CI, Nakama modules, or assets.
