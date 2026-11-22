import os

import pytest

from swarmforge_pipeline import runner


class FakeWt:
    path = os.getcwd()
    role = "falsifier"


@pytest.mark.asyncio
async def test_readonly_shell_allows_read_commands(tmp_path, monkeypatch):
    monkeypatch.setattr(runner, "_worktree_path", lambda wt: str(tmp_path))
    (tmp_path / "f.txt").write_text("hello")
    out = await runner.run_readonly_shell("cat f.txt", runner.Worktree(path=str(tmp_path), role="falsifier"))
    assert "hello" in out and "exit 0" in out
    out = await runner.run_readonly_shell("git status", runner.Worktree(path=str(tmp_path), role="falsifier"))
    assert "exit" in out


@pytest.mark.asyncio
async def test_readonly_shell_refuses_writes_and_pipes(tmp_path):
    wt = runner.Worktree(path=str(tmp_path), role="falsifier")
    for cmd in ["git commit -m x", "sf handoff t-1 -m x", "cat f > g", "cat f | grep f", "rm -rf .", "echo hi; cat f"]:
        with pytest.raises(Exception):
            await runner.run_readonly_shell(cmd, wt)


@pytest.mark.asyncio
async def test_readonly_shell_runs_argv_without_a_shell(tmp_path, monkeypatch):
    monkeypatch.setattr(runner, "_worktree_path", lambda wt: str(tmp_path))
    (tmp_path / "keep").write_text("x")
    wt = runner.Worktree(path=str(tmp_path), role="falsifier")
    for cmd in ["cat keep\ntouch pwned_newline", "env touch pwned_env", "find . -name keep -delete"]:
        with pytest.raises(Exception):
            await runner.run_readonly_shell(cmd, wt)
    assert not (tmp_path / "pwned_newline").exists()
    assert not (tmp_path / "pwned_env").exists()
    assert (tmp_path / "keep").exists()


def test_readonly_argv_allows_sf_path_forms():
    assert runner._readonly_argv("sf show t-1") == ["swarmforge/bin/sf", "show", "t-1"]
    assert runner._readonly_argv("swarmforge/bin/sf show t-1") == ["swarmforge/bin/sf", "show", "t-1"]
    assert runner._readonly_argv("/w/.worktrees/t/swarmforge/bin/sf spec-revisions t-1 --full") == [
        "/w/.worktrees/t/swarmforge/bin/sf", "spec-revisions", "t-1", "--full",
    ]


def test_readonly_argv_allows_read_commands():
    assert runner._readonly_argv("git status --porcelain") == ["git", "status", "--porcelain"]
    assert runner._readonly_argv("git log -5 --oneline") == ["git", "log", "-5", "--oneline"]
    assert runner._readonly_argv("git branch -a") == ["git", "branch", "-a"]
    assert runner._readonly_argv("grep -rn foo .") == ["grep", "-rn", "foo", "."]


def test_readonly_argv_refuses_shell_and_write_bypasses():
    for cmd in [
        "cat keep\ntouch pwned",
        "env touch pwned",
        "find . -delete",
        "find . -name x -exec rm {} +",
        "git branch -D main",
        "git branch -m main other",
        "git diff --output=x",
        "git log --output=x",
        "git commit -m x",
        "sf handoff t-1 -m x",
        "bash -c 'touch pwned'",
        "cat f > g",
        "cat f | grep f",
        "$(touch pwned)",
    ]:
        with pytest.raises(Exception):
            runner._readonly_argv(cmd)


def test_shell_env_marks_role_as_dispatched():
    env = runner._shell_env(runner.Worktree(path="/x", role="coder"))
    assert env["SWARMFORGE_DISPATCHED"] == "coder"
    assert env["SWARMFORGE_BACKEND"] == "mistral"


def test_shell_env_strips_secrets(monkeypatch):
    monkeypatch.setenv("GITHUB_TOKEN", "probe-secret")
    monkeypatch.setenv("SWARMFORGE_GITHUB_TOKEN", "probe-secret-2")
    monkeypatch.setenv("MISTRAL_API_KEY", "probe-key")
    monkeypatch.setenv("MISTRAL_CLIENT_API_KEY", "probe-key-2")
    monkeypatch.setenv("MISTRAL_SA_TOKEN_PATH", "/run/secrets/probe")
    monkeypatch.setenv("DB_PASSWORD", "probe-pw")
    env = runner._shell_env(runner.Worktree(path="/x", role="coder"))
    assert env["SWARMFORGE_DISPATCHED"] == "coder"
    assert env["SWARMFORGE_BACKEND"] == "mistral"
    assert env.get("PATH") == os.environ.get("PATH")
    for name, value in env.items():
        assert "probe" not in value, name
    for name in ("GITHUB_TOKEN", "SWARMFORGE_GITHUB_TOKEN", "MISTRAL_API_KEY", "MISTRAL_CLIENT_API_KEY", "MISTRAL_SA_TOKEN_PATH", "DB_PASSWORD"):
        assert name not in env
