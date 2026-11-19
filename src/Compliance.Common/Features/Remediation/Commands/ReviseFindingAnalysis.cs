using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Revises the team's analysis without changing the finding's source, owner or due date.</summary>
[Discriminator("bdgrz.finding.analysis.revise", 1)]
public sealed record ReviseFindingAnalysis(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Analysis, string Severity, IReadOnlyList<FindingLink> AffectedRecords)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
