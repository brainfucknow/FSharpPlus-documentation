# Real-world use cases

| Problem | Pattern | Evidence | Recipe |
|---|---|---|---|
| Optional actions; fallible configuration | `monad'`, `guard`, `tryParse >=>`, `Result` | Marksman application | [Application workflows](application-workflows.md#optional-code-actions) |
| Maintain operator-heavy mapping | `\|>>` or concrete `map` | Sharpino sample | [Application workflows](application-workflows.md#selective-mapping) |
| Thread an immutable repository | `State`, `get`/`put` | Godot playground | [State and optics](state-and-optics.md#state-threaded-repository) |
| Update nested compiler records | `_field` lenses with `<&>` | LABS implementation | [State and optics](state-and-optics.md#optics-over-language-data) |
| Accumulate independent diagnostics | Result-native `zapp`; `<!>`/`<.>` chains | SafetyFirst/LABS | [Validation and custom types](validation-and-custom-types.md#diagnostic-semantics) |
| Make domain containers composable | static members | SafetyFirst/NBB sample | [Validation and custom types](validation-and-custom-types.md#domain-containers) |
| Compose decoding and encoding | `ReaderT`, `Const`, `throw` | Fleece library | [Codecs and effects](codecs-and-composed-effects.md#codec-pattern) |
| Route async optional handlers | `OptionT<Async<_>>` | F#+ ASP.NET adapter | [Codecs and effects](codecs-and-composed-effects.md#asynchronous-optional-routing) |

These pinned sources are a small, non-representative set of implementation examples, not adoption or deployment evidence.
