# Firm methodology

Status: **locked**, 2026-11-03. Hand-authored source of truth for this context.
It follows the [context conventions](conventions.md).

## Purpose

Firm methodology keeps the firm's starting content for clients, such as controls,
policies and risks, as platform-level templates that never contain client data,
and records each time a client applies one (M0-D25, F1-04).

**Owns:** methodology templates (platform level) and template applications
(per tenant).

**Does not own:**

| Concern | Owner |
| --- | --- |
| The drafts an application creates, and their approval | Their own contexts ([Control environment](control-environment.md), [Policies](policies.md), [Risk and provider oversight](risk-and-provider-oversight.md)) |
| Criteria editions | [Criteria](criteria.md) |
| Platform operators | [Platform access](platform-access.md) |

## Language

| Term | Meaning |
| --- | --- |
| Template | A named line of methodology versions, kept at platform level. |
| Template version | One published, immutable set of entries. Each entry has a stable key and the content for one control, policy or risk. |
| Application | A tenant's use of template versions for one program. Each applied entry becomes a draft or revision proposal in the owning context, with the template version and entry key as provenance. |

## Aggregates

### MethodologyTemplate

**Identity:** `TemplateId` (platform level).

**Invariant:** one template has a line of published versions and at most one
draft. A draft is published only after a reviewer who authored no revision
accepts it and a third person, neither author nor reviewer, approves it (FM-01).
Published versions never change. A retired template takes no new versions.

- Entries never contain client data. Content from client records is never
  contributed into a template (FM-02).

```mermaid
stateDiagram-v2
    [*] --> drafting: CreateMethodologyTemplate
    drafting --> drafting: ReviseMethodologyTemplateDraft / ReviewMethodologyTemplateDraft (return)
    drafting --> reviewed: ReviewMethodologyTemplateDraft (accept)
    reviewed --> drafting: ReviseMethodologyTemplateDraft
    reviewed --> published: ApproveMethodologyTemplateVersion
    published --> drafting: ReviseMethodologyTemplateDraft
    published --> retired: RetireMethodologyTemplate
```

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| CreateMethodologyTemplate | Platform operator with `methodology.author` | `TemplateId` is new; name unused | MethodologyTemplateCreated |
| ReviseMethodologyTemplateDraft | Platform operator with `methodology.author` | Not retired; names the current draft revision; entry keys unique | MethodologyTemplateDraftRevised |
| ReviewMethodologyTemplateDraft | Platform operator with `methodology.author` who authored no revision | Drafting; reviews the current revision | MethodologyTemplateDraftAccepted or MethodologyTemplateDraftReturned |
| ApproveMethodologyTemplateVersion | Platform operator with `methodology.approve`, neither author nor reviewer | Reviewed; the accepted revision is current | MethodologyTemplateVersionPublished |
| RetireMethodologyTemplate | Platform operator with `methodology.approve` | Not retired; reason given | MethodologyTemplateRetired |

| Event | Carries |
| --- | --- |
| MethodologyTemplateCreated | template, name, purpose, created by |
| MethodologyTemplateDraftRevised | template, revision, entries (key, kind, content, criteria edition references), revised by |
| MethodologyTemplateDraftAccepted | template, revision, reviewer |
| MethodologyTemplateDraftReturned | template, revision, comments, reviewer |
| MethodologyTemplateVersionPublished | template, version, revision, entries, changed entry keys, approver |
| MethodologyTemplateRetired | template, reason, retired by |

### TemplateApplication

**Identity:** `TenantId` + `ProgramId` + `TemplateId`.

**Invariant:** one program applies one template's versions in order, each later
than the last applied. The first application creates a draft for each entry; a
later one creates revision proposals only for entries that changed, and never
overwrites approved client content (FM-03).

- Each draft or proposal takes an ID derived from the application, the version
  and the entry key ([CV-02](conventions.md#reactors)); a control, commitment or
  risk entry takes the ID its identifier derives, so an identifier already used
  in the program is refused by the owner.
- Every entry's outcome, `created`, `proposed` or `refused` with the reason, is
  recorded on the application, which is `applied` only when every entry has one
  ([CV-10](conventions.md#reactors), CV-12).
- Only the tenant's Compliance Lead applies a template (FM-04).

| Command | Who | Accepts when | Emits |
| --- | --- | --- | --- |
| ApplyMethodologyTemplate | Compliance Lead | Program active; version published; template not retired; later than the last applied version; the previous application has every entry outcome | MethodologyTemplateApplied |
| RecordTemplateEntryOutcome | The reaction that created or proposed the entry | Entry in the current application; a repeat is a no-op | TemplateEntryOutcomeRecorded |

| Event | Carries |
| --- | --- |
| MethodologyTemplateApplied | tenant, program, template, version, previous version, entries to create, entries to propose, applied by |
| TemplateEntryOutcomeRecorded | tenant, program, template, version, entry key, outcome (`created`, `proposed`, `refused` with reason), record created (if any) |

## Flows

Diagrams follow [CV-01](conventions.md#flow-diagrams).

```mermaid
flowchart LR
    C1[/ReviseMethodologyTemplateDraft/] --> E1([MethodologyTemplateDraftRevised])
    E1 -. "reviewer authored none" .-> C2[/ReviewMethodologyTemplateDraft/] --> E2([MethodologyTemplateDraftAccepted])
    E2 -. "approver is a third person" .-> C3[/ApproveMethodologyTemplateVersion/] --> E3([MethodologyTemplateVersionPublished])
    E3 -. checks .-> C4[/ApplyMethodologyTemplate/] --> E4([MethodologyTemplateApplied])
    E4 --> R1{{"Create drafts or proposals (ID from application, version, entry)"}} --> X1[[Control environment, Policies, Risk: draft or propose]]
    X1 --> C5[/RecordTemplateEntryOutcome/] --> E5([TemplateEntryOutcomeRecorded])
```

## Authorization contract

- `methodology.author` and `methodology.approve` are platform-operator
  permissions. Templates are readable by every tenant; they hold no client data.

## Disagreement and replay

Cases required by [CV-12](conventions.md#guards-and-reviews).

| Case | Outcome |
| --- | --- |
| A template control's identifier is already used in the program | The owner refuses the draft; the application records `refused` (`identifier_in_use`) |
| `MethodologyTemplateApplied` delivered twice | Entry IDs are derived, so repeats are no-ops |
| An entry's outcome is not yet recorded | The application stays incomplete, and the next version cannot be applied |

## Decisions

Jev answers are from `jev-1.14.0`, 2026-11-03. A yes is ≥ 0.8 and a no is ≤ 0.2.
Anything in between is settled by common sense, with the reasoning stated.

| ID | Decision | Evidence |
| --- | --- | --- |
| FM-01 | A template version needs a reviewer who authored none, then a third-person approver. | Reviewer alone is enough: 0.24. Third approver: 0.53. Common sense: firm methodology reaches every client, so it carries the firm's approval. |
| FM-02 | Firm staff never contribute content from client records into templates. | Contributions: 0.15 (no). A contribution keeping no client reference: 0.92, which only matters if contributions exist. |
| FM-03 | Applying a later version proposes revisions only for changed entries, never overwriting approved content. | 0.83. |
| FM-04 | Only the tenant's Compliance Lead applies a template. | Advisor applying: 0.35. Common sense: the client owns what enters its program. |

## Open questions

None at this time.

## Superseded owners

| Old owner | Becomes |
| --- | --- |
| MethodologyTemplate | MethodologyTemplate |
| MethodologyTemplateContribution | Removed (FM-02) |
| TemplateApplication | TemplateApplication; successor adoption is a later `ApplyMethodologyTemplate` (FM-03) |
| `ProposeTemplateSuccessorAdoption`, `DecideWhetherToAdoptNewTemplateVersion` | Removed: not applying a version is the decision |
