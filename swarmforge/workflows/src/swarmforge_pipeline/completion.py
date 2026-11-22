"""Completion config for role agents run by a managed workflow worker.

The Agents SDK's default Mistral provider reads MISTRAL_API_KEY as a static
key, but a managed worker has no API key: the platform mounts the deployment's
service-account token at MISTRAL_SA_TOKEN_PATH, and model calls have to go
through the worker's agent client (get_mistral_client), which authenticates
each request with that token against the worker's API URL. This module
registers a custom completion config that builds the provider with that
client on a managed worker, and with a plain MISTRAL_API_KEY locally. Import
this module in every process that builds the provider (runner.py does, so the
worker does).
"""

from __future__ import annotations

import os
from typing import ClassVar, Literal

from mistralai.vibe.harness.runtime.completion import CompletionConfigBase
from mistralai.vibe.harness.runtime.completion.profile import (
    TEXT_AND_IMAGE,
    AdapterCapabilities,
    WireFormat,
)
from mistralai.vibe.harness.runtime.completion.providers.mistral import (
    MistralCompletionProvider,
)


def _workflow_client():
    """The workflow worker's client, imported lazily: the module pulls in
    httpx, which the Temporal workflow sandbox refuses to load when it
    imports the workflow definitions at worker startup. run_as=DEPLOYMENT
    authenticates every request with the deployment's service-account token
    (MISTRAL_SA_TOKEN_PATH) against the worker's API URL, regardless of
    whether the execution runs on behalf of a user."""
    from mistralai.workflows.client import get_mistral_client
    from mistralai.workflows.core.auth.run_as import RunAs

    return get_mistral_client(run_as=RunAs.DEPLOYMENT)


class SwarmforgeWorkflowMistralConfig(CompletionConfigBase):
    """Mistral chat completions through the workflow worker's own client."""

    type: Literal["swarmforge_workflow_mistral"] = "swarmforge_workflow_mistral"
    model: str
    adapter_capabilities: ClassVar[AdapterCapabilities] = AdapterCapabilities(
        wire=WireFormat.MISTRAL,
        input_modalities=TEXT_AND_IMAGE,
        reports_cutoff=True,
        reports_usage=True,
        structured_output=True,
        max_tokens_required=False,
    )

    def build_provider(self) -> MistralCompletionProvider:
        if os.environ.get("MISTRAL_SA_TOKEN_PATH"):
            return MistralCompletionProvider(
                api_key=None,
                model=self.model,
                client=_workflow_client(),
            )
        api_key = os.environ.get("MISTRAL_CLIENT_API_KEY") or os.environ["MISTRAL_API_KEY"]
        return MistralCompletionProvider(api_key=api_key, model=self.model)
