using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     The campaign admitted one complete participant result on its open generation. The campaign's logical revision
///     advances to <paramref name="Revision" />; the participant applies the result afterwards from the frozen
///     <c>ChildAdmissionIntent</c>. The root fact carries the original 202 acknowledgment.
/// </summary>
[Discriminator("bdgrz.policy-distribution-campaign.policy-participant-result-admitted", 1)]
public sealed record PolicyParticipantResultAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
