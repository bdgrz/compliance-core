using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Raises the reassessment trigger of exactly one assessed risk affected by a changed method or boundary.
///     Dispatched only by the trusted reassessment reactors, which own the complete fan-out and derive
///     <paramref name="CommandId" /> from the source event and the risk; the owner dedupes on risk, trigger kind and
///     source reference.
/// </summary>
[Discriminator("bdgrz.risk.reassessment.raise", 2)]
public sealed record RaiseRiskReassessmentTriggers(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    string TriggerKind, string SourceReference, DateTimeOffset RaisedAt, Uuid? MethodVersionId, Uuid RiskId)
    : IRequest, IRiskReassessmentReactionRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
