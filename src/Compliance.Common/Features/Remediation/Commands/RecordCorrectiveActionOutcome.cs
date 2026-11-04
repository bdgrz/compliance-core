using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Reports one exact committed completion or cancellation to the finding that planned the action.</summary>
[Discriminator("bdgrz.finding.corrective_action.outcome.record", 1)]
public sealed record RecordCorrectiveActionOutcome(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    Uuid ActionId, string Outcome, NativeFactReference Source, ulong SourcePosition)
    : IRequest, IOperationsReactionRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
