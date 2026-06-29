# AI Workflow

## Workflow Guide Version v6.2

This repository uses workflow guide v6.2. `AGENT.md` is the local authority for pass execution rules.

## Pass Artifact Flow

Each pass should preserve the prompt, launcher, raw log, clean log, report, and metadata JSON under `build-report-logs/`. That directory is local-only and must remain gitignored.

## Prompt/Launcher/Report/Metadata Paths

- Prompts: `build-report-logs/prompts/<PASS_ID>.txt`
- Launchers: `build-report-logs/launchers/<PASS_ID>-launcher.zsh`
- Reports: `build-report-logs/reports/<PASS_ID>-report.md`
- Metadata: `build-report-logs/metadata-reports/<PASS_ID>-metadata.json`

## Agent Routing Notes

Use the lean prompt template when `AGENT.md` is present, authorized, and has a low maintenance score. Use a maintenance pass before implementation if the score reaches 5.

## P01 Bootstrap Status

P01 establishes workflow-only scaffolding, `.gitignore` hardening, project docs, local scripts, placeholders, and the .NET SDK pin. It intentionally creates no Godot project, C# project, solution, gameplay code, CI, Nakama module, or asset.

## P02 Next Pass Expectations

P02 should create the empty buildable Godot 4.7 .NET project skeleton, shared library, and first test project while preserving the Path B architecture.
