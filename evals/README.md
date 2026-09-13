# Does the skill add value?

`scripts/verify_fsharpplus_examples.py` proves the skill's examples are true.
This directory measures something different: whether a model that reads the
skill writes better F#+ code than the same model without it.

## Method

`evals.json` holds realistic tasks: extend a custom type for `map` and
`traverse`, fix a `Validation` snippet that short-circuits, build a `ReaderT`
stack, repair a value-restriction error, give a wrapper a full monad instance.
Each has `checks`, fragments that must appear in the script's printed output.

Nine practical scenarios add optional actions, mapping interop, state, optics,
validation semantics, domain containers, codecs, and async routing. Their domains
differ from the recipes. Output matching is only a smoke test, so inspect
generated source for hardcoded output and attribution claims.

`run_ab.py` runs every task through Claude Code or Codex with one model and
two configurations. The `with_skill` runs are told to read
`fsharpplus/SKILL.md` first; the `baseline` runs are not. Neither may run
dotnet, so the difference is what the skill contributes as knowledge. The
runner then compiles each `solution.fsx` with `dotnet fsi`, checks the output,
and prints pass counts per task. Configuration summaries include total
passes, total runs, pass rates, and 95% Wilson score intervals.

```sh
python3 evals/run_ab.py --model sonnet
python3 evals/run_ab.py --agent codex
python3 evals/run_ab.py --agent codex --model MODEL
python3 evals/run_ab.py --only extend-tree --repeats 2
python3 evals/run_ab.py --only extend-tree readert-stack --configs with_skill
python3 evals/run_ab.py --grade-only     # recompile every existing run
```

`--repeats N` generates `N` runs per task and configuration; its default is
one. Runs land in `evals/workspace/<eval>/<config>/run-<k>/` with the solution,
the generator result (`run.json`), the compiler output, and `grading.json`.
`--grade-only` grades every `run-*` directory it finds, independently of
`--repeats`. The workspace is ignored by git.

Claude Code is the default generator and uses `sonnet` unless `--model` is
set. Codex uses the model from its configuration unless `--model` is set.
Both generators are instructed not to run `dotnet`; Codex uses its
`workspace-write` sandbox. Codex reports token usage in `run.json`, but does
not report `cost_usd`, so that value is null.

## Reading the results

A skill earns its place when `with_skill` passes tasks `baseline` fails at a
comparable token cost. Two other outcomes are just as informative:

- A task both configurations pass is not discriminating. Replace it with a
  harder one or keep it as a regression guard.
- A task `with_skill` fails and `baseline` passes points at a sentence in the
  skill. Read `compile.log`, find the construct the model copied, and fix the
  skill text. The first pilot found two such sentences: `Map.Invoke` cited
  without `open FSharpPlus.Control`, and "annotate first" applied to a `let!`
  pattern inside `monad`.

Run with more than one model and at least twice per configuration before
trusting a small difference; single runs vary.

## Adding a task

Add an object to `evals.json` with a prompt a real user would write, any input
files under `inputs/`, and `checks` that only a correct program prints.
Prefer checks on values over checks on source text; the compiler and the
printed output are the judges.
