"""Run the pipeline on a real test worker, so the gate snapshot check and the
activity retry policy are exercised for real: a gate that changes the task
must fail its activity without retry, not pass on the retry's snapshot."""

from __future__ import annotations

from unittest.mock import patch

import pytest
from temporalio import activity as temporal_activity
from temporalio.client import WorkflowFailureError
from temporalio.converter import DataConverter
from temporalio.exceptions import ApplicationError
from temporalio.testing import WorkflowEnvironment

from mistralai.workflows.core.config.config import config
from mistralai.workflows.core.temporal.payload_converter import MistralWorkflowsPayloadConverter
from mistralai.workflows.testing import create_test_worker

from swarmforge_pipeline import activities, pipeline, runner, sfkit
from swarmforge_pipeline.catalogue import Role
from swarmforge_pipeline.models import (
    CheckoutInfo,
    JudgeVerdict,
    PipelineInput,
    RoleOutcome,
    TaskState,
)

CO = CheckoutInfo(main="/fake", worktree="/fake/.worktrees/t-1", branch="swarm/t-1")
INPUT = PipelineInput(repository="/fake/repo", task_id="t-1", remote="origin", base_branch="main")


async def test_gate_that_changes_the_task_fails_the_execution_without_retry(monkeypatch):
    agent_runs = {"n": 0}

    @temporal_activity.defn
    async def sync_checkout(params):
        return CO

    @temporal_activity.defn
    async def read_task_state(params):
        return TaskState(stage="specifier", needs="none", route=["specifier", "coder"])

    @temporal_activity.defn
    async def list_gates(params):
        return ["falsifier"]

    @temporal_activity.defn
    async def task_gates(params):
        return ["falsifier"]

    @temporal_activity.defn
    async def spec_judge_role(params):
        return "spec-judge"

    @temporal_activity.defn
    async def run_stage_role(params):
        return RoleOutcome(status="blocked", stage="specifier", needs="approval", report="spec ready")

    @temporal_activity.defn
    async def run_spec_judge(params):
        return JudgeVerdict(verdict="converged", reason="clean")

    @temporal_activity.defn
    async def publish(params):
        return "swarm/t-1"

    monkeypatch.setattr(
        activities,
        "_load_catalogue",
        lambda main: (
            [Role(name="falsifier", tier="fast", runs_as="subagent", flags=frozenset({"gate"}))],
            {},
        ),
    )
    monkeypatch.setattr(runner, "build_agent", lambda *args, **kwargs: object())

    async def fake_agent_turn(agent, role, prompt, worktree):
        agent_runs["n"] += 1
        return "gate report"

    monkeypatch.setattr(runner, "run_role_agent", fake_agent_turn)
    snapshots = iter([("state-1", "head-1", ""), ("state-2", "head-2", " M work")])
    monkeypatch.setattr(sfkit, "snapshot", lambda main, task_id: next(snapshots))

    old_deployment = config.worker.deployment_name
    old_backoff = config.worker.local_retry_backoff_coefficient
    old_exports = (
        config.common.mistral_workflows_otel_traces_export,
        config.common.mistral_workflows_otel_metrics_export,
        config.common.mistral_workflows_otel_logs_export,
    )
    config.worker.deployment_name = "test-task-queue"
    config.worker.local_retry_backoff_coefficient = 0
    config.common.mistral_workflows_otel_traces_export = False
    config.common.mistral_workflows_otel_metrics_export = False
    config.common.mistral_workflows_otel_logs_export = False
    try:
        data_converter = DataConverter(payload_converter_class=MistralWorkflowsPayloadConverter)
        with patch("mistralai.workflows.core.tracing.utils.temporalio.workflow.upsert_search_attributes"):
            async with await WorkflowEnvironment.start_time_skipping(data_converter=data_converter) as env:
                async with create_test_worker(
                    env,
                    [pipeline.SwarmforgePipeline],
                    activities=[
                        sync_checkout,
                        read_task_state,
                        list_gates,
                        task_gates,
                        spec_judge_role,
                        run_stage_role,
                        activities.run_gate,
                        run_spec_judge,
                        publish,
                    ],
                ):
                    handle = await env.client.start_workflow(
                        "swarmforge-pipeline", INPUT, id="wf-gate-violation", task_queue="test-task-queue"
                    )
                    with pytest.raises(WorkflowFailureError) as err:
                        await handle.result()
    finally:
        config.worker.deployment_name = old_deployment
        config.worker.local_retry_backoff_coefficient = old_backoff
        (
            config.common.mistral_workflows_otel_traces_export,
            config.common.mistral_workflows_otel_metrics_export,
            config.common.mistral_workflows_otel_logs_export,
        ) = old_exports

    assert agent_runs["n"] == 1, "the gate activity was retried after changing the task"
    violation = None
    cause = err.value
    while cause is not None:
        if isinstance(cause, ApplicationError) and cause.type == "gate-changed-task":
            violation = cause
        cause = getattr(cause, "cause", None)
    assert violation is not None, f"no gate-changed-task ApplicationError in {err.value!r}"
    assert violation.non_retryable
