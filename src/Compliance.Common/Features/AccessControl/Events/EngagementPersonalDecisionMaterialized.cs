using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The materialized personal decision was recorded on the engagement: the content pin is released and the
///     engagement's current personal decision pointer advances unless a later decision already holds it. It does not
///     advance the engagement revision.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.engagement-personal-decision-materialized", 1)]
public sealed record EngagementPersonalDecisionMaterialized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
