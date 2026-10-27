using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The immutable historical preview of one admitted evaluation, materialized from the frozen intent only. The
///     complete <see cref="IndependenceEvaluationView" /> is immutable content; the preview never grants production
///     acceptance. The evaluation identity is <paramref name="OperationId" />.
/// </summary>
[Discriminator("bdgrz.independence.evaluated", 1)]
public sealed record ClientIndependenceEvaluated(Uuid TenantId, Uuid OperationId,
    ImmutableContentRef EvaluationContent, OperationProvenance Operation, ulong Revision, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
