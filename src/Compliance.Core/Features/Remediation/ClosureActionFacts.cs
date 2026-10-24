using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     What a closure reads from one action: its owner and, when completed, the completer, the completion fact and the
///     evidence manifest, with the closer and whether an exact waiver proof covers the closer. These are the required
///     facts of the closure use and are verified content. <paramref name="Refusal" /> is the permanent reason the action
///     cannot be held for this closure, or null.
/// </summary>
public sealed record ClosureActionFacts(Uuid ActionId, Uuid OwnerMemberId, bool Completed, Uuid? CompletedByMemberId,
    NativeFactReference? CompletionFact, ImmutableContentRef? EvidenceManifest, Uuid CloserMemberId, bool WaiverProven,
    string? Refusal);
