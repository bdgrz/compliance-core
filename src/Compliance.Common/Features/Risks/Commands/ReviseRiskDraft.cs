using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Risks.Commands;

[Discriminator("bdgrz.risk.draft.revise", 1)]
public sealed record ReviseRiskDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, string Title, string Scenario, string PotentialEffect,
    string? SourceNote = null) : IRequest,
    IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
