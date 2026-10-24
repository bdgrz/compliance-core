using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The participant's applied results are held for the campaign closure. The use does not advance the public
///     participant revision; <paramref name="Revision" /> is the held revision.
/// </summary>
[Discriminator("bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-authorized", 1)]
public sealed record PolicyCampaignParticipantCampaignClosureUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
