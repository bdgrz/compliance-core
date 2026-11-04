# Canonical methodology application ordering and completion

Tracking #1176. Source prepared 2026-11-04 against the locked Firm methodology context. **Authored, uncompiled and unexecuted** in this worker lane. Tests-only preparation commits are not observed RED. Coordinator must run the focused selection and full assembled gate; no acceptance/merge/deployment is claimed.

## Owner and behavior

TemplateApplication now retains a canonical V4 stream at UUIDv5(TenantId, program/template), independent of the old ApplicationKey. Its existing legacy fact decoder remains available at old stream identities. New commands cannot append through the legacy mutation path on a canonical owner. One V4 apply records a strictly later published version only after its predecessor completes; exact old command retries remain fact-free and changed intent conflicts. The parent records just application/completion business facts for each version. The child TemplateVersionApplication retains per-entry outcomes and a frozen exact approved destination base before proposal dispatch.

ApplyMethodologyTemplateV4Handler checks current personal client membership, current role-specific Compliance Lead authority and authoritative ProgramApplicationAdmission on every call including receipt retries. AppliedBy in the committed V4 event is the original **UserId**, retained for the destination reaction to reauthorize the same person. ProgramApplicationAdmission is supplied by the Programs worker; the locked Program has no retirement/ending lifecycle, so that worker explicitly qualifies active tenant plus authoritative creation rather than inventing a status.

New applications read real V4 publications from MethodologyTemplateLineReader's authoritative snapshots. They compare the entire selected publication against the actual last-applied publication, preserving changes across skipped versions. New entry keys create drafts; changed existing keys propose revisions; unchanged keys produce no destination write. Removed keys do not overwrite or remove approved client content. Exact content and the original publication source/physical position are frozen on the applied fact. The executor checks that the preceding applied version did not change while that plan was prepared.

OpenTemplateVersionApplicationV4 resolves the exact committed canonical application fact before opening the child. The child retains both the actual application EventId and the stable application CommandId. The command binding is propagated on its completion fact. RecordTemplateApplicationCompleted resolves the exact child completion source and matches its tenant/program/template/version/command binding. The parent validates nonnegative totals summing to its planned entries, and permits only the in-flight version. Exact completion retries remain no-ops. Empty plans complete through the child owner's existing zero-entry completion behavior.

The V4 opening/completion reactors map only committed source facts to deterministic commands; Portia owns delivery/retry/checkpoints. A new GetTemplateApplicationV4 query reads authoritative current progress and final counts from the canonical owner.

## Source preparation tests

TemplateApplicationOrderingTests covers incomplete-predecessor refusal/no append, increasing/skipped version ordering, exact historical retry versus changed intent, immutable completion counts/source binding, canonical tenant isolation, replay, retained proposal-base selection, full publication differences across skipped versions, and current client Lead/firm/guest/suspended/ended authority. These tests have not been executed or compiled in this worker lane. No green count is recorded.

## Bounded Jev signal

Live TypeSafe System One request resolved `jev-latest` to `jev-1.14.0` (HTTP 200). Adverse probabilities: next-before-completion .12; same/backward new version .16; old exact retry appends .17; skipped-publication differences lost .27; arbitrary completion counts accepted without source resolution .03; non-Lead allowed .09; frozen proposal base can change .05. The .27 answer remains advisory uncertainty; a focused source-preparation regression names skipped-version changes, unchanged keys, new keys and removed keys. Runtime proof remains pending. Requests/responses: `/tmp/compliance-template-application-ordering-jev-request.json`, `/tmp/compliance-template-application-ordering-jev-response.json`. Key was held only in the calling process environment.

## Exact shared integration instructions

Coordinator owns all shared files; this packet does not edit them.

- Register handlers ApplyMethodologyTemplateV4Handler, OpenTemplateVersionApplicationV4Handler, RecordTemplateApplicationCompletedHandler, GetTemplateApplicationV4Handler.
- Register ApplyMethodologyTemplateV4Authorizer as a service (also directly injected into its handler) and request authorizer; register TemplateApplicationV4ReactionAuthorizer for its two reaction commands. TimeProvider and existing membership/role permission services are required.
- Register TemplateVersionApplicationV4OpeningReactor and TemplateApplicationCompletionReactor as tenant-scoped workloads.
- Register ProgramApplicationAdmission from the Programs worker; do not replace it with a projection status lookup.
- Add JSON metadata/closed request/response types for ApplyMethodologyTemplateV4, OpenTemplateVersionApplicationV4, RecordTemplateApplicationCompleted, GetTemplateApplicationV4, TemplateApplicationProgressView, MethodologyTemplateAppliedV4, TemplateApplicationCompleted, TemplateEntryProposalBase and TemplateEntryProposalPrepared. Existing TemplateVersionApplicationOpened/Completed gain optional ApplicationCommandId metadata. SourceFactProof<MethodologyTemplateAppliedV4> and SourceFactProof<TemplateVersionApplicationCompleted> use standard source-fact proof metadata as required by the shared context's conventions.
- Map public apply/read transport to the V4 commands/query and retire the old ApplicationKey apply/successor-adoption public admission. Retain legacy history decoding. The new request uses explicit CommandId and has no ApplicationKey.
- Regenerate event/aggregate inventories only from the coordinator lane.

## Coordinated capability dependencies

Actual destination create/propose/refuse execution is being implemented by the parent worker in the same feature. It must freeze PreparedProposalFor before proposal dispatch, use the committed AppliedBy original user with current authorization, and record only authoritative destination outcomes. The arbitrary legacy RecordTemplateEntryOutcome admission must refuse V4 children (ApplicationCommandId nonempty). Source-backed success provenance, permanent-refusal handling and transport wiring must be integrated before enabling V4 application publicly.

A source read of nonretired global methodology followed by tenant application cannot itself make retirement/application atomic across streams; no projection check or framework was introduced to imply that guarantee. The locked context supplies no retirement/application reservation protocol. Current authoritative source admission is explicit, while concurrency and any further cross-owner business reservation qualification remain review items for assembled acceptance.
