# FSharpPlus-documentation
An installable agent skill for the **FSharpPlus (F#+)** library for F#, supporting **Codex, Claude Code and GitHub Copilot**.

## Install

See [INSTALL.md](INSTALL.md) for project-local paths, commands for Bash and PowerShell, activation, updates and removal. Copy the complete [fsharpplus/](fsharpplus/) folder. Consuming projects do not need SwarmForge.

## Build the portable archive

From the repository root, use Python 3 to package only the skill, installation guide and license:

```bash
mkdir -p tmp
python3 -m zipfile -c tmp/fsharpplus-skill.zip fsharpplus INSTALL.md LICENSE
```

Distribute `tmp/fsharpplus-skill.zip`; extract it before following the installation guide. Keep generated files and caches outside `fsharpplus/`. The archive excludes the repository workflow and evaluation tools.

## Verification

Run `python3 scripts/verify_fsharpplus_examples.py` to compile every F# example in the skill. The verifier requires each normal example to compile, each intentionally failing SRTP example to fail with its documented F# diagnostic code, and every `// expect:` line inside an example to appear in that example's output. Pass `--dotnet /path/to/dotnet` when `dotnet` is not on `PATH`.

## Evaluation

Run `python3 evals/run_ab.py` to compare the same model with and without the skill on the tasks in `evals/evals.json`. See `evals/README.md` for the method and how to read the results.

See the [real-world use-case recipes](fsharpplus/references/real-world-use-cases.md) for practical, source-backed patterns.
