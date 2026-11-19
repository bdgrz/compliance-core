# Runtime criteria readiness follow-up

Status: **open; runtime-selected readiness remains pending**. The runtime
criteria coverage query can verify a bounded catalog page against the exact
publication and the pre-publication committed area prefix. This is not a
complete bounded readiness-assessment implementation.

The existing readiness format is an independently larger migration:

- `RunReadinessAssessmentHandler.FreezeAsync` serializes the entire selected
  criteria set, mappings, effective controls and other source families into
  immutable array objects referenced by `ReadinessInputSnapshot` v1. It
  currently rejects runtime selections rather than inventing a complete input.
- `ComputeReadinessAssessmentHandler.EvaluateAsync` loads those whole objects.
  `ReadinessRules.Evaluate` constructs full findings and gaps lists, a mapping
  lookup and a growing fingerprint string. Merely chunking the criteria input
  would still load and produce unbounded objects.
- The computation writes complete findings and gaps arrays to individual
  content references. `ReadinessReader` subsequently reloads those complete
  arrays even for paged queries, annotation and gap selection. These must be
  migrated too; a chunked input alone would not satisfy bounded content or
  processing.

A future v2 must define source-cut-bound immutable input items or bounded linked
chunks, with durable progress and no unbounded parent roster; stream each exact
published criterion and its mapping inputs into a deterministic incremental
fingerprint and evaluation; write independent bounded findings/gaps and paged
indexes; and teach all readiness readers, annotation/decision consumers and
recovery paths to read the new structure incrementally. There is no current
application or backwards-compatibility requirement, so the existing format
can be replaced directly; the fixed seed is fresh initial data. Add the
required source-generated JSON/event registrations, freeze the selected
edition/publication and as-of semantics, and prove complete
settlement before claiming a complete assessment. A source or participant still
pending remains pending/incomplete; no empty projection or partial scan becomes
ready. This work is not a prerequisite for individual publication or program
creation, and no v2 schema or implementation is claimed here.

The legacy `ListCriteriaCoverage` result embeds every mapped control for one
criterion in a single row. Runtime criteria enumeration and mapping scans now
retain only one requested criteria page, but one criterion can still have many
mapped controls. A truly bounded response for that case needs a separately
paged mapping-detail contract; the current row format cannot express its own
continuation. Until that API migration, neither the row's output size nor its
per-page mapping accumulation has a strict bound.
