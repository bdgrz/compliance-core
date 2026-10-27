using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The root native acceptance of a client independence preview on the service-epoch owner. It freezes the
///     complete preview intent at <paramref name="Revision" />, the service epoch at admission, and does not advance
///     the epoch. The preview itself is materialized later by <c>ClientIndependencePreviewProcess</c>.
/// </summary>
[Discriminator("bdgrz.client-service-independence.client-independence-evaluation-admitted", 1)]
public sealed record ClientIndependenceEvaluationAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
