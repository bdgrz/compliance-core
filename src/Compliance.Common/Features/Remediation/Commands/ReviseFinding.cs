using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Changes severity, owner, due date, affected scope, or root cause with a reason.</summary>
[Discriminator("bdgrz.finding.revise", 2)]
public sealed record ReviseFinding(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Severity, Uuid OwnerMemberId, DateOnly DueOn,
    string AffectedScope, string? RootCause, string Reason)
    : IRequest<FindingView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
