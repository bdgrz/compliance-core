# Focused Jev event-name review

Use this reference when a slice adds or changes a business event name.

## Review state

Provide only enough context for a person to judge the name quickly:

- exact product requirement/source wording, including source ID and file/section;
- aggregate owner, invariant, and identity;
- exact completed business fact and its trigger/producer;
- sibling facts and lifecycle sequence that could be confused with it;
- event payload's business meaning, excluding metadata and protected values;
- current name and a few alternatives that preserve exactly the same source-backed meaning.

Do not send broad repositories, full catalogs, credentials, protected data, raw vendor values, or unrelated history. Do not ask Jev to decide aggregate ownership, approve policy, infer authority, or implement behavior.

## Questions

Ask separately:

1. Does the current name clearly and precisely denote this completed fact?
2. If not, which source-preserving candidate is clearest?
3. Does the alternative retain the exact approved fact and remain distinct from sibling events?
4. Is the judgment uncertain because the source language is underspecified?

For Choice, every option needs clear criteria. Pair a Choice with a Noul that asks whether any candidate fits. Keep options semantically distinct. Avoid compound questions and generic “is this good?” prompts.

## Adjudication

- High confidence is useful evidence, not approval. Check the answer against the cited source, command meaning, aggregate transitions, schema and sibling names.
- Low confidence or close probabilities means unresolved; improve context or preserve the current name pending source clarification.
- Do not choose a prettier name that implies agency, consent, approval, finality, settlement, revocation, or completion not present in the source.
- Record the exact model/version, question, answer distribution/confidence, request/response paths, and human source-based disposition.
- If no naming defect exists, record no change. Do not rename for stylistic preference alone.
