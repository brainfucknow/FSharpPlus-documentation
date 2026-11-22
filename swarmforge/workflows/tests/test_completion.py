import os
import subprocess
import sys
from pathlib import Path

import pytest
from mistralai.vibe.harness.runtime.completion import parse_completion_config
from mistralai.vibe.harness.runtime.completion.providers.mistral import (
    MistralCompletionProvider,
)

from swarmforge_pipeline import completion, runner
from swarmforge_pipeline.catalogue import Role


def test_config_registers_and_round_trips():
    parsed = parse_completion_config(
        {"type": "swarmforge_workflow_mistral", "model": "mistral-medium-latest"}
    )
    assert isinstance(parsed, completion.SwarmforgeWorkflowMistralConfig)
    assert parsed.model == "mistral-medium-latest"


def test_build_provider_uses_api_key_without_sa_token(monkeypatch):
    monkeypatch.delenv("MISTRAL_SA_TOKEN_PATH", raising=False)
    monkeypatch.delenv("MISTRAL_CLIENT_API_KEY", raising=False)
    monkeypatch.setenv("MISTRAL_API_KEY", "test-key")
    provider = completion.SwarmforgeWorkflowMistralConfig(model="mistral-medium-latest").build_provider()
    assert isinstance(provider, MistralCompletionProvider)
    assert provider.model == "mistral-medium-latest"
    assert provider._api_key == "test-key"
    assert provider._client is None


def test_build_provider_uses_workflow_client_with_sa_token(monkeypatch):
    monkeypatch.setenv("MISTRAL_SA_TOKEN_PATH", "/tmp/sa-token")
    sentinel = object()
    monkeypatch.setattr(completion, "_workflow_client", lambda: sentinel)
    provider = completion.SwarmforgeWorkflowMistralConfig(model="mistral-large-latest").build_provider()
    assert isinstance(provider, MistralCompletionProvider)
    assert provider.model == "mistral-large-latest"
    assert provider._api_key is None
    assert provider._client is sentinel


def _role() -> Role:
    return Role(name="architect", tier="frontier", runs_as="agent")


def test_workflow_module_chain_does_not_import_completion():
    src = str(Path(completion.__file__).parent)
    code = (
        "import sys\n"
        "import swarmforge_pipeline.pipeline\n"
        "print('LOADED' if 'swarmforge_pipeline.completion' in sys.modules else 'CLEAN')\n"
    )
    env = {**os.environ, "PYTHONPATH": src + os.pathsep + os.environ.get("PYTHONPATH", "")}
    proc = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, env=env)
    assert proc.returncode == 0, proc.stderr
    assert proc.stdout.strip() == "CLEAN"


def test_build_agent_pins_the_completion_config():
    agent = runner.build_agent(_role(), "mistral-large-latest", "instructions", writable=True)
    assert isinstance(agent.completion, completion.SwarmforgeWorkflowMistralConfig)
    assert agent.completion.model == agent.model == "mistral-large-latest"


def test_build_agent_rejects_mismatched_model():
    with pytest.raises(ValueError, match="must match Agent.model"):
        runner.agents.Agent(
            model="mistral-medium-latest",
            name="x",
            completion=completion.SwarmforgeWorkflowMistralConfig(model="mistral-large-latest"),
        )
