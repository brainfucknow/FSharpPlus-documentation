"""Worker entrypoint: registers both SwarmForge workflows.

Run locally with MISTRAL_API_KEY set: uv run python -m swarmforge_pipeline.worker
On a managed worker no API key is needed: role agents authenticate model calls
with the deployment service-account token at MISTRAL_SA_TOKEN_PATH through the
workflow worker's client (see completion.py).
"""

from __future__ import annotations

import asyncio

import mistralai.workflows as workflows

from .pipeline import SwarmforgePipeline, SwarmforgeRole

WORKFLOWS = [SwarmforgePipeline, SwarmforgeRole]


async def main() -> None:
    await workflows.run_worker(WORKFLOWS)


if __name__ == "__main__":
    asyncio.run(main())
