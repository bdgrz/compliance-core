using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     The participant's closure use ended with the campaign's terminal fact. A settlement before any grant fences
///     the key, so a delayed acquisition is refused.
/// </summary>
[Discriminator("bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-settled", 1)]
public sealed record PolicyCampaignParticipantCampaignClosureUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
