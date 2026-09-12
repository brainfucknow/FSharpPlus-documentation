# FSharpPlus-documentation
FSharpPlus documentation

## Verification

Run `python3 scripts/verify_fsharpplus_examples.py` to compile every F# example in the skill. The verifier requires each normal example to compile, each intentionally failing SRTP example to fail with its documented F# diagnostic code, and every `// expect:` line inside an example to appear in that example's output. Pass `--dotnet /path/to/dotnet` when `dotnet` is not on `PATH`.

## Evaluation

Run `python3 evals/run_ab.py` to compare the same model with and without the skill on the tasks in `evals/evals.json`. See `evals/README.md` for the method and how to read the results.

Problem-oriented, source-backed recipes live in [`fsharpplus/references/real-world-use-cases.md`](fsharpplus/references/real-world-use-cases.md). They distinguish observations, verified 1.9.1 adaptations, and model results. Run `python3 scripts/verify_fsharpplus_examples.py`; the separate A/B method is in [`evals/README.md`](evals/README.md).
