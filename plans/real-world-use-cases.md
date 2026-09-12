# Real-world use-case implementation record

This file records the implementation of the evidence-led plan supplied on
2026-09-12. The checkout was the requested `122e421` baseline. The historical
gist was accessible and describes the original evidence-first, compile-every-
example process; it reinforces rather than changes the repository rules.

## Baseline

- Checkout: `122e42110d1c039a59fbc797ec3e96598799d074`.
- SDK installed outside the repository for verification: .NET SDK 8.0.425.
- Initial verifier: blocked because `dotnet` was not on `PATH`; after installing
  it in `/tmp/dotnet`, all 13 original example groups passed.
- Evidence was reopened at each pinned commit. The manifest records exact
  symbols, classifications, caveats, versions when established, and licenses.
- Adaptations target FSharpPlus 1.9.1 and are original stand-ins, not copied
  downstream implementations.

## Evaluation status

The nine scenarios were added without altering the five regressions. Paid
`claude -p` generation was **not run** because no authorization was supplied.
That is not an evaluation pass and no model-improvement claim is made. Example
verification and scenario capability remain separate evidence.
