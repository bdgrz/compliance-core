using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     The root native acceptance of a campaign closure. It seals result admission on the open generation and
///     freezes the complete admitted participant results and the exact participant uses to hold.
/// </summary>
[Discriminator("bdgrz.policy-distribution-campaign.policy-campaign-closure-accepted", 1)]
public sealed record PolicyCampaignClosureAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
