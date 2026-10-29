---
id: make-the-fsharpplus-skill-pack-d0c3059
created: 2026-10-29
stage: done
route: specifier,coder,cleaner,architect,hardener,qa
---

# Make the FSharpPlus skill pack installable in F#+ projects

Using swarm forge setup in your environment I want you to create skill files that can be installed in F#+ projects. F#+ is a library for F#. Assess the existing FSharpPlus skill pack and deliver a reusable, verified project-installable skill pack with clear installation instructions.

## Records

```text
type: created
from: operator
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: a6ef822
route: specifier,coder,cleaner,architect,hardener,qa
```

```text
type: review
from: specifier
commit: a6ef822
files: -
summary: Portable FSharpPlus skill criteria and independent installation QA ready for approval
```

```text
type: review
from: specifier
commit: 3564530
files: -
summary: Portable FSharpPlus skill criteria and installation QA committed for approval
```

```text
type: changes-requested
from: operator
reason: Support Codex, Claude Code and GitHub Copilot. Include project-local installation instructions and independent QA for all three. Preserve the rest of the proposed specification.
```

```text
type: review
from: specifier
commit: ace27e9
files: -
summary: Codex, Claude Code and GitHub Copilot installation criteria ready
```

```text
type: approval
from: operator
```

```text
type: handoff
from: specifier
to: coder
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: ace27e9
files: -
summary: specifier work complete
```

```text
type: handoff
from: coder
to: cleaner
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: f0dba06
files: INSTALL.md,README.md,fsharpplus/LICENSE,fsharpplus/SKILL.md
summary: Portable FSharpPlus skill and three-client installation guide ready; all 38 example groups pass
```

```text
type: handoff
from: cleaner
to: architect
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: 1055f75
files: -
summary: Portable skill documentation reviewed; no cleanup needed and example verification reused
```

```text
type: handoff
from: architect
to: hardener
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: 49293e2
files: -
summary: Portable skill and client installation boundaries reviewed; no structural change needed
```

```text
type: handoff
from: hardener
to: qa
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: 78a4def
files: -
summary: Installation guidance hardened by review; no applicable executable mutation target and existing example evidence reused
```

```text
type: handoff
from: qa
to: done
branch: swarm/make-the-fsharpplus-skill-pack-d0c3059
commit: 55b955c
files: -
summary: Portable archive and all three documented client installation lifecycles independently verified; 38 passing example groups reused
```
