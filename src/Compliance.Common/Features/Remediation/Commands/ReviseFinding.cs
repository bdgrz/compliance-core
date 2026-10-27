using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Changes severity, owner, due date, affected scope, or root cause with a reason.</summary>
[Discriminator("bdgrz.finding.revise", 2)]
public sealed record ReviseFinding(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Severity, Uuid OwnerMemberId, DateOnly DueOn,
    string AffectedScope, string? RootCause, string Reason)
    : IRequest<FindingView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
