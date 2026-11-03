# User journeys and flows

Status: product-level flow map, derived from the product brief, accepted
decisions, and backlog. Story contracts and live delivery state remain owned by
their linked product documents and GitHub issues.

This document diagrams the complete product journey and its connected user
flows. **MVP** means the first client can complete the manual readiness
workflow and establish a Type I plan in an isolated organization. **Later**
marks follow-on examination, Type II, firm, commercial, or optional integration
work. A diagram records intended behavior; its presence does not claim that the
flow is implemented or accepted.

Every flow inherits tenant isolation, server-side authorization, attribution,
provenance, history, and human approval rules. Optional imports and integrations
enter the same governed review path as manually entered records. M0 decisions
and EN foundation stories support the flows but are not standalone user
journeys.

## End-to-end compliance journey

The same program and its governed history carry forward across readiness, Type
I, and Type II. The first release ends at the Type I entry plan; examination
flows below are later milestones.

```mermaid
flowchart LR
  A[Create isolated organization] --> B[Invite team and grant scoped access]
  B --> C[Create program, choose criteria, and define boundary]
  C --> D[Author workforce, system, obligation, risk, and provider records]
  D --> E[Define controls, policies, owners, and cadence]
  E --> F[Operate controls, reviews, and evidence workflows]
  F --> G[Run reproducible readiness assessment]
  G --> H[Assign and resolve readiness gaps]
  H --> G
  G --> I[Management records Type I entry decision]
  I -->|Defer or conditional| H
  I -->|Proceed| J[Type I engagement and point-in-time snapshot]
  J --> K[Auditor requests, samples, and observations]
  K --> L[Validate package and record separate management and auditor outcomes]
  L --> M[Start Type II period from approved baseline]
  M --> N[Operate recurring controls and reviews]
  N --> O[Freeze period snapshot and complete Type II examination]
  O --> P[Record outcome and roll program into next period]
```

## MVP: organization, program, and readiness

### 1. Provision an organization and establish team access

**Stories:** R1-04 and R1-04a–e, R1-15, BO-01.

```mermaid
flowchart TD
  A[Account holder creates client organization] --> B[Platform creates tenant and slug]
  B --> C[Create first organization administrator]
  C --> D[Invite personnel through configured identity provider]
  D --> E[Invitee authenticates and accepts]
  E --> F[Administrator grants organization and program scope]
  F --> G[Assign members to platform-managed teams]
  G --> H{Assignment or approval has a SoD conflict?}
  H -->|No| I[Member works within granted scope]
  H -->|Yes| J[Block or route for an authorized independent decision]
  J --> I
  I --> K{Member suspended or removed?}
  K -->|No| I
  K -->|Yes| L[Revoke access and reassign orphaned work]
  L --> M[Delegated administrator continues organization work]
```

### 2. Set up the SOC 2 program, criteria, and system boundary

**Stories:** R1-01, R1-02, R1-03.

```mermaid
flowchart TD
  A[Compliance lead creates program] --> B[Choose traceable criteria edition]
  B --> C[Define in-scope services and system boundary]
  C --> D[Record intended audit scope and exclusions]
  D --> E[Review boundary, criteria, and unresolved facts]
  E --> F{Review accepted?}
  F -->|No| G[Revise draft and preserve prior version]
  G --> E
  F -->|Yes| H[Approve versioned boundary and criteria selection]
  H --> I[Show setup work and next required records]
```

### 3. Build the manual readiness context

**Stories:** R1-07, R1-10, R1-11, R1-12, R1-13, R1-14.

```mermaid
flowchart LR
  A[Approved program scope] --> B[Record workforce and work relationships]
  A --> C[Record service identities and accountable owners]
  A --> D[Inventory applications and reviewed system instances]
  A --> E[Inventory technology and information assets]
  A --> F[Record commitments, requirements, CUECs, and CSOCs]
  A --> G[Record providers and subservice organizations]
  A --> H[Assess scoped risks and select treatments]
  B --> I[Validate links, ownership, source, and effective history]
  C --> I
  D --> I
  E --> I
  F --> I
  G --> I
  H --> I
  I --> J[Review unresolved or unknown facts]
  J --> K[Use governed records as versioned readiness inputs]
```

### 4. Define controls and map applicable criteria

**Stories:** R1-05, R1-06; ownership and cadence continue in R2-01.

```mermaid
flowchart TD
  A[Selected criteria edition] --> B[Draft control and implementation narrative]
  B --> C[Map control to criteria and explain applicability]
  C --> D[Link scope, risks, commitments, assets, and providers]
  D --> E[Review design and mapping]
  E --> F{Approved?}
  F -->|No| G[Revise and retain prior version]
  G --> E
  F -->|Yes| H[Assign accountable owner and operating cadence]
  H --> I[Create governed control occurrences]
```

### 5. Complete readiness assessment and own the gap plan

**Stories:** R1-08; owned work is supported by R2-11.

```mermaid
flowchart TD
  A[Select approved program scope and as-of time] --> B[Run versioned readiness rules]
  B --> C[Freeze exact rule and source input versions]
  C --> D[Show satisfied, missing, stale, and unassessed rules]
  D --> E{Readiness gaps remain?}
  E -->|No| F[Management reviews reproducible assessment]
  E -->|Yes| G[Annotate gaps and select source workflow]
  G --> H[Assign owner, action, and target date]
  H --> I[Resolve source record or complete corrective work]
  I --> J[Attach and review supporting evidence]
  J --> K[Re-run assessment against new as-of inputs]
  K --> D
  F --> L[Use assessment snapshot in Type I entry decision]
```

### 6. Record the Type I entry decision

**Stories:** R2-09. This is a management decision, separate from any auditor
conclusion. The diagram shows the intended outcome; routing deferred or
conditional decisions into shared findings, responsibility, and work remains
open under #297.

```mermaid
flowchart TD
  A[Management selects completed readiness assessment] --> B[Review frozen snapshot, rule version, inputs, and gaps]
  B --> C[Record attributed Type I entry decision]
  C --> D{Decision}
  D -->|Defer| E[Record rationale and owned follow-up actions]
  E --> F[Resolve actions in their owning workflows]
  F --> A
  D -->|Conditional| G[Record conditions, owners, and due dates]
  G --> H[Track condition evidence and reassess]
  H --> A
  D -->|Proceed| I[Preserve immutable decision snapshot and Type I plan]
  I --> J[Begin separate Type I engagement workflow when scheduled]
```

### 7. Bring existing readiness material into the program

**Story:** R1-09. This is an optional path for a later client; it is not needed
for the first client's manual workflow.

```mermaid
flowchart TD
  A[Select supported source material] --> B[Stage records and files without changing the program]
  B --> C[Preview mappings, duplicates, and validation results]
  C --> D{Conflicts or rejected rows?}
  D -->|Yes| E[Resolve or exclude rows with reasons]
  E --> C
  D -->|No| F[Authorized user accepts the preview]
  F --> G[Atomically create governed records with source provenance]
  G --> H[Reconcile readiness to accepted records]
  H --> I[Repeat safely without duplicating accepted facts]
```

## MVP: operating controls and supporting workflows

### 8. Operate a control, submit evidence, review, and remediate

**Stories:** R2-01, R2-03, R2-04, R2-05a–d, R2-07.

```mermaid
flowchart TD
  A[Owner receives scheduled control occurrence] --> B[Perform the control and record result]
  B --> C[Open evidence request or attach governed evidence]
  C --> D[Inspect and make artifact available for review]
  D --> E[Reviewer evaluates design, implementation, and evidence]
  E --> F{Conclusion}
  F -->|Satisfactory| G[Record independent review and close occurrence]
  F -->|Deviation or evidence gap| H[Create finding with source and severity]
  H --> I[Assign corrective action owner and due date]
  I --> J[Owner changes source control or completes remediation]
  J --> K[Submit new evidence and request retest]
  K --> E
  G --> L[Retain occurrence, decision, evidence links, and history]
```

### 9. Govern evidence from request through retention and disclosure

**Stories:** R2-03, R2-12.

Production capture, storage, and inspection still have open acceptance work
(#196, #272); this diagram shows the complete user workflow those slices need
to support.

```mermaid
flowchart TD
  A[Authorized user opens evidence request] --> B[Owner submits artifact or records a governed reference]
  B --> C[Store immutable content identity and provenance]
  C --> D[Inspect artifact]
  D --> E{Inspection result}
  E -->|Unsafe or unknown| F[Quarantine and reconcile inspection or storage effect]
  F -->|Retry or release after authorized review| D
  E -->|Available| G[Link exact artifact revision to supported record]
  G --> H[Reviewer reads artifact through scoped authorization]
  H --> I[Apply retention period and any legal or examination hold]
  I --> J{Authorized disclosure requested?}
  J -->|Yes| K[Issue attributable, time-bounded download]
  J -->|No| L[Retain until policy permits disposition]
  K --> L
  L --> M[Expire or dispose only after holds and retention rules allow]
```

### 10. Maintain, publish, and acknowledge policies

**Stories:** R2-02, R2-10.

```mermaid
flowchart TD
  A[Policy owner drafts or revises policy] --> B[Review policy version and audience]
  B --> C{Approved by authorized reviewer?}
  C -->|No| A
  C -->|Yes| D[Publish approved version]
  D --> E[Notify assigned audience]
  E --> F[Members acknowledge or complete required training]
  F --> G[Record attributed response and evidence]
  G --> H{Missing, rejected, or overdue response?}
  H -->|Yes| I[Create owned follow-up work and escalate by policy]
  I --> F
  H -->|No| J[Review policy on its recurring cycle]
  J --> A
```

### 11. Reconcile access and complete an access review

**Stories:** R1-10c, R2-06a–e.

```mermaid
flowchart TD
  A[Compliance lead approves reviewed-system scope] --> B[Record and accept point-in-time access population]
  B --> C[Freeze population snapshot]
  C --> D[Classify human and non-human principals]
  D --> E[Resolve identity, owner, group, role, and grant path]
  E --> F[Approve access expectations]
  F --> G[Compare actual access with expectations]
  G --> H[Launch review campaign against frozen population]
  H --> I[Reviewer decides retain, change, or revoke]
  I --> J{Reviewer is conflicted or access should change?}
  J -->|Conflict| K[Route to independent authorized reviewer]
  K --> I
  J -->|No change| L[Record decision and rationale]
  J -->|Change needed| M[Create remediation work for access owner]
  M --> N[Owner changes access in its source system]
  N --> O[Verify with a later population snapshot]
  O -->|Still present| M
  O -->|Resolved| L
  L --> P[Complete campaign with decisions and evidence]
```

### 12. Collaborate with a readiness advisor

**Story:** R2-08.

```mermaid
flowchart TD
  A[Client grants engagement-scoped advisor access] --> B[Advisor reviews permitted readiness work]
  B --> C[Advisor adds comment, change request, or validation feedback]
  C --> D[Client owner reviews feedback]
  D --> E{Client accepts requested change?}
  E -->|No| F[Record response and rationale]
  E -->|Yes| G[Client changes record in its owning workflow]
  G --> H[Re-run affected review or readiness assessment]
  F --> I[Preserve authorship and engagement boundary]
  H --> I
```

### 13. Manage accountable work and reminders

**Stories:** R2-11a–c. Work remains owned by the source workflow that created
it.

```mermaid
flowchart TD
  A[Gap, finding, request, review, or policy creates work] --> B[Project source-backed item to personal and team queues]
  B --> C[Owner claims or authorized lead assigns work]
  C --> D{Conflict, overload, or missed due date?}
  D -->|No| E[Owner completes action in source workflow]
  D -->|Conflict| F[Block or route through SoD-aware delegation]
  F --> C
  D -->|Overdue| G[Send configured reminder or escalation]
  G --> C
  E --> H[Source workflow records outcome and evidence]
  H --> I[Queue reflects completion from source facts]
```

## Later: examination, Type II, firm, and commercial journeys

### 14. Establish and complete a Type I examination

**Stories:** T1-01–07. This follows the MVP Type I entry plan.

```mermaid
flowchart TD
  A[Open Type I engagement from approved entry decision] --> B[Freeze point-in-time engagement snapshot]
  B --> C[Author and approve system description]
  C --> D[Grant auditor least-privilege engagement access]
  D --> E[Respond to auditor requests and samples]
  E --> F[Reconcile complete populations and supporting evidence]
  F --> G[Record observations and audit findings separately]
  G --> H[Validate indexed Type I package]
  H --> I[Management records its sign-off]
  I --> J[Auditor outcome is recorded as auditor-authored]
  J --> K[Approve Type II operating plan from outcome and findings]
```

### 15. Operate the Type II observation period

**Stories:** T2-01–09, including T2-08a–b. Source integrations are optional
collection paths and retain manual operation.

```mermaid
flowchart TD
  A[Start period from approved Type I baseline] --> B[Schedule recurring control occurrences]
  B --> C[Perform controls and collect occurrence evidence]
  C --> D[Run recurring access, policy, risk, provider, and management reviews]
  D --> E[Assess significant changes and incidents for compliance impact]
  E --> F[Monitor readiness and surface aging or new gaps]
  F --> G[Assign and resolve source-owned follow-up work]
  G --> B
  A --> H[Optionally collect from cloud or customer-hosted sources]
  H --> I[Reconcile observations into the same governed review path]
  I --> C
  F --> J[Close period and prepare immutable examination snapshot]
```

### 16. Close and examine a Type II period, then roll forward

**Stories:** T3-01–07.

```mermaid
flowchart TD
  A[Close observation period] --> B[Freeze examination snapshot]
  B --> C[Prove complete populations]
  C --> D[Respond to auditor samples and evidence requests]
  D --> E{Exception or missing support?}
  E -->|Yes| F[Record exception and corrective action]
  F --> G[Verify remediation and preserve original period facts]
  G --> D
  E -->|No| H[Validate indexed Type II package]
  H --> I[Management records sign-off]
  I --> J[Record auditor-authored outcome]
  J --> K[Roll program forward with history and next-period baseline]
```

### 17. Serve multiple clients as a firm

**Stories:** F1-01–08. This is later than the first isolated client workflow.

```mermaid
flowchart TD
  A[Firm administrator onboards or suspends client organization] --> B[Client data and authorization remain tenant-scoped]
  B --> C[Assign firm staff and client-specific capacity]
  C --> D[Show authorized client portfolio and work queue]
  D --> E[Apply versioned firm template with provenance]
  E --> F[Client performs work in its owning organization]
  A --> G[Record advisory or attest service engagement]
  G --> H[Check independence before accepting assignment]
  H --> I{Engagement and separation rules satisfied?}
  I -->|No| J[Block or revise assignment]
  J --> G
  I -->|Yes| K[Grant engagement-scoped access]
  K --> D
  B --> L[Client identity provider may be configured in F1]
  B --> M[Offboard with governed export and retention]
```

### 18. Administer hosted billing and optional add-ons

**Stories:** BO-02–03. Self-hosted manual use remains free and does not require
this flow.

```mermaid
flowchart TD
  A[Organization chooses self-hosted or hosted operation] --> B{Hosted?}
  B -->|No| C[Use complete manual workflow without billing]
  B -->|Yes| D[Authorized client or provider administers organization billing]
  D --> E[Apply approved base subscription policy]
  E --> F{Select optional add-on?}
  F -->|No| G[Continue manual operation]
  F -->|Yes| H[Review add-on scope, authority, entitlement, and price]
  H --> I{Accepted and billable?}
  I -->|No| G
  I -->|Yes| J[Enable selected integration, service, or automation]
  J --> K[Keep manual owning workflow available]
```

## Story coverage

| Product scope | Diagrams |
| --- | --- |
| R1-01–03 program, criteria, and boundary | 2 |
| R1-04, R1-04a–e, R1-15, BO-01 organization and access | 1 |
| R1-05–07, R1-10–14 readiness source records | 3–4 |
| R1-08 readiness and gap plan; R2-09 Type I entry | 5–6 |
| R1-09 optional import | 7 |
| R2-01, R2-03–05, R2-07 control operation and remediation | 8 |
| R2-03, R2-12 evidence governance | 9 |
| R2-02, R2-10 policies | 10 |
| R1-10c, R2-06 access reviews | 11 |
| R2-08 advisor collaboration | 12 |
| R2-11 accountable work | 13 |
| T1-01–07 Type I examination | 14 |
| T2-01–09 Type II operation and optional collection | 15 |
| T3-01–07 Type II examination and rollover | 16 |
| F1-01–08 firm operations | 17 |
| BO-02–03 hosted commercial operations | 18 |

EN-01–06 authorization, history, snapshots, decisions, imports, and artifact
storage are cross-cutting foundations. They apply within the relevant flows
above rather than forming separate end-user journeys.
