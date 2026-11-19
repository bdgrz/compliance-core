# Criteria edition selected-operation review

Status: **partial, not qualified for completion**. The edition owner now selects
one operation before replay and retains only that operation's intent, outcome,
fence, abort and fixed participant-stage evidence. Its current label,
publication transition and other lifecycle facts remain scalar state. It does
not retain a collection of criteria or a dictionary of historical operation IDs.
An unselected owner can read current state but refuses mutations. Each caller
must select the exact operation or publication attempt before hydration.

Focused behavioral tests were authored for changed intent, refusal and
confirmation retries, separate cancellation and original-operation receipts,
and unselected writes. They have **not been run**. The aggregate is not done:
Portia still loads the edition's full event stream; selecting one operation only
bounds retained operation-specific state. The lifetime and churn of this stream
need a deliberate domain decision and source qualification. Exact cross-owner
participant facts and current authorization still require trustworthy source
reads, and the existing provider freshness limitation remains open.

Selecting one ID means replay verifies the exact selected operation's history;
an unselected current-state replay is not a general audit that all historical
IDs are globally unique. A producer must always select the operation it will
mutate, and an authoritative history repair/audit must not infer global
uniqueness from an unselected replay. Same-text entry-summary operations still
need a durable operation-owned intent and receipt if they are to return a
retryable success without recording a redundant domain change.

## Jev review

The exact source and questions were sent as the retained [request](criteria-edition-selected-operation-jev-request.json);
the [response](criteria-edition-selected-operation-jev-response.json) is from
`jev-1.14.0`. Probabilities are advisory and do not prove implementation or
provider guarantees. The initial API request received HTTP 403 because its key
header was empty; the subsequent authenticated request succeeded. No key was
included in either retained artifact.

| Question | Probability of yes |
| --- | ---: |
| Selected replay avoids Portia's full stream load | 0.05 |
| An unselected owner can safely accept a mutation | 0.07 |
| Retained operation state grows with distinct historical operation IDs | 0.09 |
| Selecting the cancelled original preserves its refused receipt | 0.93 |
| Selecting the cancellation preserves its accepted receipt independently | 0.80 |
| Changed authored intent is an exact retry | 0.18 |
| Same participant event with changed label generation is an exact retry | 0.16 |
| Same participant event with changed refusal reason is an exact retry | 0.16 |
| Unselected replay proves uniqueness across the full historical stream | 0.03 |
| Ordinary replay proves freshness after an ambiguous commit | 0.08 |
| The owner must retain all criteria entries to decide its lifecycle | 0.06 |
| A no-op can have a durable exact-retry outcome with no intent or receipt persisted anywhere | 0.48 |

The no-op answer is inconclusive. The implementation must not claim a durable
receipt without recording evidence elsewhere. Real provider-backed races,
replay, and the focused tests remain necessary before drawing a conclusion.
