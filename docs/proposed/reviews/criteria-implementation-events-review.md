# Criteria implementation event review

Status: **event-name review in progress**, 2026-11-05. The source owners are
being implemented. This review checks event language against the selected
[criteria context](../contexts/criteria.md); it does not prove handlers, source
authority, persistence, replay, or the complete feature. Test and compilation
runs are deferred until the whole implementation chunk is assembled.

## Claim-owner facts

The live Jev 1.14.0 [request](criteria-implementation-events-jev-label-request.json)
and [response](criteria-implementation-events-jev-label-response.json) use the
selected per-label exclusivity rule and the proposed source event meanings.

| Question | Probability |
| --- | ---: |
| `EditionLabelReserved` names a provisional exclusive claim | 0.96 |
| `EditionLabelConfirmed` names its confirmation against the edition fact | 0.94 |
| `EditionLabelReleased` names relinquishing the exact generation | 0.91 |
| An old release can clear a different current generation under the stated check | 0.04 |
| An unverified event ID alone proves the edition source fact | 0.03 |
| An unknown response authorizes releasing a reservation | 0.04 |
| A label owner must keep a list of all editions to decide its own claim | 0.09 |

The last three questions are authority and boundary counterexamples, not
evidence the current handler already performs the necessary source checks.

## Edition and entry facts

The [edition and entry request](criteria-implementation-events-jev-edition-request.json)
and [response](criteria-implementation-events-jev-edition-response.json) judged
the individual pending, accepted, refused and cleanup facts. Jev rated
`CriteriaEditionDraftRequested` 0.89, `CriteriaEditionDrafted` 0.97,
`CriteriaPublicationStarted` 0.96, `CriteriaEditionPublished` 0.94, and
`CriteriaPublicationAbortCleanupCompleted` 0.97. It rated the proposed,
changed, refused, witness-pinned and witness-fenced entry names 0.93–0.98.
It rated an abort fact alone proving witness cleanup 0.03 and an unverified
event ID proving an exact committed source fact 0.02.

The [remaining transitions request](criteria-implementation-events-jev-transitions-request.json)
and [response](criteria-implementation-events-jev-transitions-response.json)
cover edition attempt fencing, initial-label selection and refusal, cancellation,
rename stages, draft abandonment, entry attempt fencing, and summary correction.
The stated event names scored 0.84–0.97 for their supplied meanings. These
scores are signals about phrasing, not proofs that the stated facts are produced
or safely consumed.

## Intermediate activation wording

`CriteriaEditionDraftActivated` records an intermediate edition-side step before
the independent claim confirmation, not a ready-to-edit draft. The initial Jev
question about whether its name might imply readiness was inconclusive at 0.47.
A narrower [follow-up request](criteria-implementation-events-jev-activation-request.json)
and [response](criteria-implementation-events-jev-activation-response.json) rated
the existing name as clearly conveying the still-pending state at 0.15, versus
0.79 for `CriteriaEditionDraftLabelAttached`; the latter is a possible wording
improvement, not an approved discriminator change. A separate old-name question
was inconclusive at 0.48. Preserve the selected source contract unless the owner
and coordinator intentionally reconcile all producers, consumers and the context;
in all cases the runtime must expose **activating/pending** until the later
`CriteriaEditionDrafted` fact and must not permit edits or publication early.

No model answer replaces a committed exact-source read, the participant's
durable outcome, an expected-position fence, or focused runtime tests. Record the
actual implementation and verification results separately once available.
