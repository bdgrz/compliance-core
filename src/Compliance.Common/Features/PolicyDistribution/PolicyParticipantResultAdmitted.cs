using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The campaign admitted one complete participant result on its open generation. The campaign's logical revision
///     advances to <paramref name="Revision" />; the participant applies the result afterwards from the frozen
///     <c>ChildAdmissionIntent</c>. The root fact carries the original 202 acknowledgment.
/// </summary>
[Discriminator("bdgrz.policy-distribution-campaign.policy-participant-result-admitted", 1)]
public sealed record PolicyParticipantResultAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
