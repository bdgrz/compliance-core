using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The root native acceptance of a personal engagement decision on the engagement owner. It freezes the complete
///     decision intent at <paramref name="Revision" />, the engagement content revision at admission, and pins that
///     content until the decision is materialized or canceled. It does not advance the engagement revision.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.engagement-personal-decision-admitted", 1)]
public sealed record EngagementPersonalDecisionAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
