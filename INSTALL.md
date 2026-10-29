# Install the F#+ skill

This pack teaches an AI coding agent how to use the **FSharpPlus (F#+)** library for F#. It includes generic-function lookup, namespace resolution, SRTP diagnostics, computation expressions, transformers, custom types, optics, validation and source-backed recipes. Copy the whole `fsharpplus/` directory, including `references/` and `LICENSE`; `SKILL.md` alone is incomplete.

Installing the skill adds agent guidance to your repository. It does not install a NuGet package or change your project's dependencies. SwarmForge and this documentation repository's verification tools are not needed in the consuming project.

## Choose a client location

Paths are relative to the root of the consuming repository.

| Client | Project-local skill file | Use |
|---|---|---|
| Codex | `.agents/skills/fsharpplus/SKILL.md` | Start Codex in the repository; ask it to use `$fsharpplus` for your task. |
| Claude Code | `.claude/skills/fsharpplus/SKILL.md` | Start Claude Code in the repository; invoke `/fsharpplus` with your task, or name the skill in your prompt. |
| GitHub Copilot | `.github/skills/fsharpplus/SKILL.md` | Use an agent-skills-capable Copilot client. In Copilot CLI, use `/skills reload`, then `/skills info fsharpplus`; ask it to use `/fsharpplus`. |

All three use the same skill files. No client-specific rewrite is needed. In a multi-client repository, Copilot also documents `.agents/skills` and `.claude/skills`, so it can reuse the Codex or Claude Code copy on supported surfaces. You do not need another Copilot copy when your client already discovers that location. Codex plus Claude Code can use their respective copies; keep them at the same pack revision. Avoid installing the same skill into multiple locations searched by one client.

Copilot's official documentation lists CLI, cloud agent, code review and agent mode in supported IDEs; IDE support depends on the client version. This pack targets clients that support `SKILL.md` agent skills. It does not imply that every Copilot surface, ordinary chat mode or inline completion loads skills. Check the [feature matrix](https://docs.github.com/en/copilot/reference/copilot-feature-matrix) for your version, and enable agent skills if your IDE exposes a setting or organization policy for them. For a remote/cloud agent, commit the installed folder to the repository it runs against.

## Install from the ZIP or a checkout

Extract `fsharpplus-skill.zip`. The extracted directory contains `INSTALL.md`, `LICENSE` and `fsharpplus/`. In the commands below, set `pack` to that extracted directory (or this repository checkout) and `project` to your consuming repository. Choose **one** `skill_parent` from the location table.

Bash (macOS, Linux, WSL or Git Bash):

```bash
pack="/path/to/extracted pack"
project="/path/to/My FSharp Project"
skill_parent="$project/.agents/skills"       # Codex
# skill_parent="$project/.claude/skills"     # Claude Code
# skill_parent="$project/.github/skills"     # Copilot

(
    set -eu
    test -f "$pack/fsharpplus/SKILL.md"
    test -d "$project"
    destination="$skill_parent/fsharpplus"
    if [ -e "$destination" ] || [ -L "$destination" ]; then
        printf '%s\n' "Already exists: $destination. See Update below."
        exit 1
    fi
    mkdir -p "$skill_parent"
    cp -R "$pack/fsharpplus" "$destination"
)
```

PowerShell:

```powershell
$pack = 'C:\Downloads\extracted pack'
$project = 'C:\Source\My FSharp Project'
$skillParent = Join-Path $project '.agents/skills'       # Codex
# $skillParent = Join-Path $project '.claude/skills'     # Claude Code
# $skillParent = Join-Path $project '.github/skills'     # Copilot
$destination = Join-Path $skillParent 'fsharpplus'
if (!(Test-Path -LiteralPath (Join-Path $pack 'fsharpplus/SKILL.md') -PathType Leaf)) {
    throw 'The pack must contain fsharpplus/SKILL.md.'
}
if (!(Test-Path -LiteralPath $project -PathType Container)) { throw 'Project not found.' }
if (Test-Path -LiteralPath $destination) { throw 'Destination exists. See Update below.' }
New-Item -ItemType Directory -Path $skillParent -Force -ErrorAction Stop | Out-Null
Copy-Item -LiteralPath (Join-Path $pack 'fsharpplus') -Destination $destination -Recurse -ErrorAction Stop
```

Open a new agent session after copying, or use your client's skill-reload command. Confirm that `fsharpplus` is available using the client's skill listing/menu. Then try: “Use the fsharpplus skill to explain when to use Validation instead of Result in this F#+ project.” Agents may also select it automatically from its description when a task involves F#+. Discovery and automatic selection depend on the client, settings and prompt; having the files on disk alone does not demonstrate live discovery.

If discovery fails, check the repository root, exact `SKILL.md` capitalization, complete folder copy, skill enablement and supported client version. Do not put this pack in a custom-agent directory such as `.github/agents`.

## Update and remove

If a destination already exists, inspect it for local edits before replacing it. Back up that **specific `fsharpplus` folder** outside all skill-search directories, remove or move the old folder, then repeat installation with the new complete folder. A clean replacement removes stale references that an overlay copy could leave behind. Do not delete the parent `skills` directory or other skills. Update every separate copy you maintain, and reload the agent session afterward.

To uninstall, remove only the installed `fsharpplus` directory at the location you chose. Leave other skills, instruction files, NuGet configuration and F# sources intact. Reload/restart the client. Removing a copy shared with Copilot also removes that shared skill for Copilot.

## Scope, sources and license

The examples target **FSharpPlus 1.9.1**, with verification pinned to **.NET SDK 8.0.425**. Expected compiler diagnostics may differ with another SDK. When working in a project with another F#+ version, check its installed API and compile against that project before applying a recipe. Skill installation itself does not require that exact SDK.

The repository's existing example verifier compiles examples, checks expected output and checks deliberately failing SRTP diagnostics. This is separate from live client discovery and model-quality evaluation; authenticated A/B model runs are optional. The recipe provenance remains in `fsharpplus/references/use-case-sources.json` and the pinned source links in the recipes.

The pack's MIT license is included at `LICENSE` and inside `fsharpplus/LICENSE`, so it travels with an installed skill. External sources retain their own licenses.

Client-location and skill-format sources reviewed on 2026-10-29:

- [Codex skills documentation](https://developers.openai.com/codex/skills), linked by the [official Codex repository](https://github.com/openai/codex/blob/main/docs/skills.md); the [official project-root discovery source](https://github.com/openai/codex/blob/main/codex-rs/ext/skills/src/host_roots.rs) uses `.agents/skills`.
- [Claude Code skills documentation](https://code.claude.com/docs/en/skills); the [official Claude Code changelog](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md) documents project `.claude/skills` discovery and skill menus, and [Anthropic's skill examples](https://github.com/anthropics/skills) document the portable format.
- [GitHub Copilot agent skills](https://docs.github.com/en/copilot/concepts/customizations/agent-skills), [CLI setup](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills) and the [official shared setup source](https://github.com/github/docs/blob/main/data/reusables/copilot/creating-adding-skills.md).

The official GitHub-hosted source files were checked for this pack. Actual client installation/discovery must be checked in the client you use; clients and documentation can change.
