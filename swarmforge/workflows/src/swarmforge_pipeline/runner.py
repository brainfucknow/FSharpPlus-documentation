"""Run one SwarmForge role as an Agents SDK agent inside a workflow activity.

The agent gets worker-side tools scoped to the task worktree through a session
input, so the model cannot touch paths outside it. Role agents may write and
run shell; gate and spec-judge agents get a read-only shell only. Every shell
runs with SWARMFORGE_DISPATCHED and SWARMFORGE_BACKEND set, so sf refuses the
operator-only commands inside the worker exactly as it does for a local
dispatched role."""

from __future__ import annotations

import os
import shlex
import subprocess
from typing import Annotated

from mistralai import agents

from .catalogue import Role


class Worktree(agents.SessionInput):
    """Which task worktree this session works in, and as which role."""

    input_id = "swarmforge.worktree"
    path: str
    role: str


def _worktree_path(wt: Worktree) -> str:
    root = os.path.realpath(wt.path)
    if not os.path.isdir(root):
        raise agents.ToolError(f"worktree {wt.path} does not exist")
    return root


def _resolve(wt: Worktree, path: str) -> str:
    root = _worktree_path(wt)
    full = os.path.realpath(os.path.join(root, path))
    if full != root and not full.startswith(root + os.sep):
        raise agents.ToolError(f"path {path} escapes the task worktree")
    return full


_SECRET_NAME_PARTS = ("TOKEN", "SECRET", "PASSWORD", "API_KEY", "PRIVATE_KEY", "CREDENTIAL")


def _shell_env(wt: Worktree) -> dict[str, str]:
    """The environment of an agent shell. It drops every variable whose name
    looks secret (tokens, keys, passwords, credentials): only the worker's
    own publish step needs those, and the agent must not be able to read or
    send them."""
    env = {
        name: value
        for name, value in os.environ.items()
        if not any(part in name.upper() for part in _SECRET_NAME_PARTS)
    }
    env["SWARMFORGE_DISPATCHED"] = wt.role
    env["SWARMFORGE_BACKEND"] = "mistral"
    return env


def _run(command: str, wt: Worktree) -> str:
    proc = subprocess.run(
        ["bash", "-c", command],
        cwd=_worktree_path(wt),
        capture_output=True,
        text=True,
        timeout=600,
        env=_shell_env(wt),
    )
    out = (proc.stdout + proc.stderr).strip()
    return f"exit {proc.returncode}\n{out}" if out else f"exit {proc.returncode}"


def _run_argv(argv: list[str], wt: Worktree) -> str:
    """Run an argv without a shell, so no metacharacter can turn one command
    into several."""
    try:
        proc = subprocess.run(
            argv,
            cwd=_worktree_path(wt),
            capture_output=True,
            text=True,
            timeout=600,
            env=_shell_env(wt),
        )
    except FileNotFoundError:
        raise agents.ToolError(f"command not found: {argv[0]}")
    out = (proc.stdout + proc.stderr).strip()
    return f"exit {proc.returncode}\n{out}" if out else f"exit {proc.returncode}"


@agents.tool
async def run_shell(command: str, wt: Annotated[Worktree, agents.Depends()]) -> str:
    """Run a shell command in the task worktree and return its combined output
    and exit code. Use it for swarmforge/bin/sf commands, tests, and git. The
    command runs with SWARMFORGE_DISPATCHED set to your role, so sf refuses the
    operator-only commands."""
    return _run(command, wt)


_READONLY_SF = {"show", "ls", "stage", "next", "spec-revisions", "route", "harvest", "roles", "gates", "routes", "pipeline", "check", "--help"}
_READONLY_GIT = {"log", "diff", "status", "show", "rev-parse", "branch", "ls-files", "ls-remote", "blame", "describe"}
_READONLY_UNIX = {"cat", "ls", "grep", "egrep", "fgrep", "head", "tail", "wc", "file", "tree", "pwd", "which"}
_READONLY_FORBIDDEN = ("&&", "||", ";", "|", ">", "<", "`", "$(", "\n", "\r")
_READONLY_GIT_FORBIDDEN_ARGS = {
    "-o", "-c", "-D", "-d", "-m", "-M", "-f",
    "--delete", "--move", "--force", "--exec", "--output",
    "--git-dir", "--work-tree", "--config",
}


def _readonly_argv(command: str) -> list[str]:
    """The argv a read-only command maps to, or a refusal. The command is
    parsed without a shell, so separators and substitutions are never
    executed, and only whitelisted programs with whitelisted subcommands run."""
    stripped = command.strip()
    if any(token in stripped for token in _READONLY_FORBIDDEN):
        raise agents.ToolError(f"read-only shell refuses '{command}': no pipes, redirections, or command substitution")
    try:
        parts = shlex.split(stripped)
    except ValueError as exc:
        raise agents.ToolError(f"read-only shell cannot parse '{command}': {exc}")
    if not parts:
        raise agents.ToolError("empty command")
    prog = parts[0]
    if prog == "sf" or prog == "swarmforge/bin/sf" or prog.endswith("/swarmforge/bin/sf"):
        if len(parts) > 1 and parts[1] in _READONLY_SF:
            return ["swarmforge/bin/sf" if prog == "sf" else prog, *parts[1:]]
    elif prog == "git" and len(parts) > 1 and parts[1] in _READONLY_GIT:
        if any(arg in _READONLY_GIT_FORBIDDEN_ARGS or arg.startswith("--output") for arg in parts[2:]):
            raise agents.ToolError(f"read-only shell refuses '{command}': that git option writes")
        return parts
    elif prog in _READONLY_UNIX:
        return parts
    raise agents.ToolError(
        f"read-only shell refuses '{command}': allowed are sf {sorted(_READONLY_SF)}, "
        f"git {sorted(_READONLY_GIT)}, and {sorted(_READONLY_UNIX)}"
    )


@agents.tool
async def run_readonly_shell(command: str, wt: Annotated[Worktree, agents.Depends()]) -> str:
    """Run a read-only inspection command in the task worktree: swarmforge/bin/sf
    show/ls/stage/spec-revisions/route/harvest (also as bare sf), git
    log/diff/status/show/rev-parse/branch/ls-files/ls-remote/blame/describe,
    or basic unix readers (cat, ls, grep, head, tail, ...). The command runs as
    an argv without a shell; anything else, and any piping, redirection, or
    writing option, is refused."""
    return _run_argv(_readonly_argv(command), wt)


@agents.tool
async def read_file(path: str, wt: Annotated[Worktree, agents.Depends()]) -> str:
    """Read a file, given as a path relative to the task worktree root."""
    with open(_resolve(wt, path), encoding="utf-8") as fh:
        return fh.read()


@agents.tool
async def write_file(
    path: str, content: str, wt: Annotated[Worktree, agents.Depends()]
) -> str:
    """Write a whole file, given as a path relative to the task worktree root."""
    full = _resolve(wt, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8") as fh:
        fh.write(content)
    return f"wrote {path}"


def build_agent(role: Role, model: str, instructions: str, writable: bool) -> agents.Agent:
    from .completion import SwarmforgeWorkflowMistralConfig

    shell_tool = run_shell if writable else run_readonly_shell
    tools = [shell_tool, read_file] + ([write_file] if writable else [])
    return agents.Agent(
        model=model,
        name=f"swarmforge-{role.name}",
        instructions=instructions,
        tools=tools,
        completion=SwarmforgeWorkflowMistralConfig(model=model),
    )


async def run_role_agent(
    agent: agents.Agent, role: str, prompt: str, worktree: str
) -> str:
    """One agent turn on the prompt; returns the final text. Raises on a
    non-completed turn so the activity retries or fails."""
    async with agents.local_runtime():
        session = await agent.start_session(
            session_inputs={Worktree.input_id: Worktree(path=worktree, role=role)}
        )
        state = await session.run(prompt)
        await session.cancel()
    if isinstance(state, agents.Completed):
        return state.text
    if isinstance(state, agents.Failed):
        raise RuntimeError(f"agent turn failed: {state.error}")
    raise RuntimeError(f"agent turn ended with outcome {type(state).__name__}")
