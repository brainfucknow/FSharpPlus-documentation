# FSharpPlus-documentation
FSharpPlus documentation

## Verification

Run `python3 scripts/verify_fsharpplus_examples.py` to compile every F# example in the skill. The verifier requires each normal example to compile, each intentionally failing SRTP example to fail, and each failure to emit its documented F# diagnostic code. Pass `--dotnet /path/to/dotnet` when `dotnet` is not on `PATH`.
