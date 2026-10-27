using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     The pending closure can definitely never complete. The cancellation fences every delayed local finalization of
///     the generation and reopens result admission on the next generation.
/// </summary>
[Discriminator("bdgrz.policy-distribution-campaign.policy-campaign-closure-canceled", 1)]
public sealed record PolicyCampaignClosureCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
