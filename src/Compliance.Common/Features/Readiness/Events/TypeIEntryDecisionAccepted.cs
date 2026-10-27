using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     The root native acceptance of a Type I entry decision. It seals the latest adoption generation and the gap plans
///     and references the frozen <see cref="ReadinessDecisionIntent" />; the decision is only recorded once
///     <see cref="TypeIEntryDecisionRecorded" /> follows.
/// </summary>
[Discriminator("bdgrz.program-readiness.type-i-entry-decision-accepted", 1)]
public sealed record TypeIEntryDecisionAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
