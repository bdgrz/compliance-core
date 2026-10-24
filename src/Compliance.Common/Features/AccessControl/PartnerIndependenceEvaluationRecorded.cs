using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The immutable personal partner evaluation, materialized from the authorized personal decision. The complete
///     <see cref="PartnerIndependenceEvaluationView" /> and the ratified rules it relied on are immutable content; the
///     evaluation identity is <paramref name="OperationId" />.
/// </summary>
[Discriminator("bdgrz.service-engagement.partner-evaluation.recorded", 1)]
public sealed record PartnerIndependenceEvaluationRecorded(Uuid OperationId, ImmutableContentRef EvaluationContent,
    ImmutableContentRef RatifiedRulesContent, OperationProvenance Operation, ulong Revision,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
