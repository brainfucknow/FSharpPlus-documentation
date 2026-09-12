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

`run_ab.py` runs every task twice through `claude -p` with one model: the
`with_skill` run is told to read `fsharpplus/SKILL.md` first, the `baseline`
run is not. Neither may run dotnet, so the difference is what the skill
contributes as knowledge. The runner then compiles each `solution.fsx` with
`dotnet fsi`, checks the output, and prints one row per run plus a
fully-passing count per configuration.

```sh
python3 evals/run_ab.py --model sonnet
python3 evals/run_ab.py --only extend-tree readert-stack --configs with_skill
python3 evals/run_ab.py --grade-only     # recompile existing runs
```

Runs land in `evals/workspace/<eval>/<config>/` with the solution, the
`claude -p` result (`run.json`), the compiler output, and `grading.json`.
The workspace is ignored by git.

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
