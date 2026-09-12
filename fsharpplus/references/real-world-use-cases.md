# Real-world use cases

These are a small, non-representative set of pinned implementation observations.
Each recipe separates **observed usage**, an original **distilled adaptation**,
and any **measured benefit**. No comparative model run has yet measured benefit.

| Problem | Pattern | Evidence | Recipe |
|---|---|---|---|
| Optional actions; fallible configuration | `monad'`, `guard`, `Result` | Marksman application (U1) | [Application workflows](application-workflows.md#u1-optional-code-actions) |
| Maintain operator-heavy mapping | `|>>` or concrete `map` | Sharpino sample (U2) | [Application workflows](application-workflows.md#u2-selective-mapping) |
| Thread an immutable repository | `State` | Godot playground (U3) | [State and optics](state-and-optics.md#u3-state-threaded-repository) |
| Update nested compiler records | lenses | LABS implementation (U4) | [State and optics](state-and-optics.md#u4-optics-over-language-data) |
| Accumulate independent diagnostics | applicative `Validation` | SafetyFirst/LABS (U5) | [Validation and custom types](validation-and-custom-types.md#u5-diagnostic-semantics) |
| Make domain containers composable | static members | SafetyFirst/NBB sample (U6) | [Validation and custom types](validation-and-custom-types.md#u6-domain-containers) |
| Compose decoding and encoding | `ReaderT`, `Const` | Fleece library (U7) | [Codecs and effects](codecs-and-composed-effects.md#u7-codec-pattern) |
| Route async optional handlers | `OptionT<Async<_>>` | F#+ ASP.NET adapter (U8) | [Codecs and effects](codecs-and-composed-effects.md#u8-asynchronous-optional-routing) |

Source details and attribution boundaries are in
[`use-case-sources.json`](use-case-sources.json). A dependency/import is not
deployment evidence, and a verified adaptation is not a downstream build.
