# FSharpPlus-documentation
FSharpPlus documentation

## Verification

Run `python3 scripts/verify_fsharpplus_examples.py` to compile every F# example in the skill. The verifier requires each normal example to compile, each intentionally failing SRTP example to fail with its documented F# diagnostic code, and every `// expect:` line inside an example to appear in that example's output. Pass `--dotnet /path/to/dotnet` when `dotnet` is not on `PATH`.

## Evaluation

Run `python3 evals/run_ab.py` to compare the same model with and without the skill on the tasks in `evals/evals.json`. See `evals/README.md` for the method and how to read the results.

See the [real-world use-case recipes](fsharpplus/references/real-world-use-cases.md) for practical, source-backed patterns.
