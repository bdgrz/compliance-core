using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The decision was canceled before authorization: it fences authorization and releases the engagement content
///     pin. An authorized decision recovers forward and is never canceled.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.engagement-personal-decision-canceled", 1)]
public sealed record EngagementPersonalDecisionCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
