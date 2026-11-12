# AGENTS.md

Instructions for AI coding agents working in this repository. Humans can follow them too.

## Project

BitPack is a bit-level serialization library for .NET. A Roslyn source generator reads `[BitPacket]` types and emits `Serialize`, `Deserialize`, span, delta and union code at compile time. It is a research project. See [README.md](README.md) for usage and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the pieces fit.

| Directory | Role | Target |
| :--- | :--- | :--- |
| `BitPack/` | Runtime library: bit I/O, attributes | `netstandard2.1`, `net10.0` |
| `BitPack.Generator/` | Source generator, split into `PacketGenerator.*.cs` partials | `netstandard2.0` |
| `BitPack.Tests/` | xUnit tests, including `WireFormatTests` that pin exact bytes | `net10.0` |
| `BitPack.Benchmarks/` | BenchmarkDotNet harness | `net10.0` |

## Setup

Requires the .NET 10 SDK. Install the git hooks once per clone:

```bash
dotnet tool restore
dotnet husky install
```

The pre-commit hook builds the solution in Release and runs the tests. Do not bypass it with `--no-verify`.

## Checks

Run these before every commit. CI runs the same commands.

```bash
dotnet build BitPack.slnx -c Release
dotnet test BitPack.slnx -c Release --no-build
```

`TreatWarningsAsErrors` is on in every project, so a new warning fails the build.

## Workflow for each task

1. **Use a git worktree.** Create one per task so parallel tasks do not share a working tree, and branch from the latest `master`:
   ```bash
   git fetch origin
   git worktree add ../BitPack-<topic> -b <type>/<topic> origin/master
   ```
2. **Make the change.** Keep the diff focused on the task.
3. **Run the checks** above. Fix failures before committing.
4. **Commit in logical groups.** One concern per commit, for example tests, generator change and docs as separate commits. Messages are short imperative subjects that say what changed ("Add union delta test"), with a body only when the reason is not obvious.
5. **Open a pull request right away** when the work is done: push the branch and run `gh pr create --base master`. The description says what changed and what was left alone.
6. **Wait for CI.** Do not stop at opening the PR.
   - If CI passes, the task is done. Report the PR link.
   - If CI fails because of the change, fix it, push, and wait again. Make at most 3 fix attempts.
   - If CI still fails after 3 attempts, or fails for a reason unrelated to the change (a flaky runner, an outage, a permissions error, a broken `master`), stop and tell the user what failed and the link to the run.

## Keep docs current

A change is not done until the docs match it. Before opening the PR, check that the following still describe the code:

- `README.md`: usage, attribute tables, wire-format rules, benchmark notes.
- `docs/ARCHITECTURE.md`: layers, the generator file table, known limits.
- `AGENTS.md`: commands, directory table, workflow.

Add or change a generator file, attribute, diagnostic or public API, and update the matching docs in the same PR.

## Writing style

Docs, comments and commit messages are written plainly: no filler, no inflated vocabulary, no em dashes, no recap of what was just said. Run the `unslop-text` skill over any prose you add or change.

## Rules for the code

- Behavior changes to the wire format are breaking. Update `WireFormatTests` deliberately and say so in the PR.
- New generator features need a test in `BitPack.Tests` and, if they add an attribute, a README entry.
- The library targets `netstandard2.1` and `net10.0`, and the generator targets `netstandard2.0`. Do not use APIs newer than the target.
- Do not edit `VERSION` or publish to NuGet. Releases run from the `publish.yml` workflow, started by a maintainer.
