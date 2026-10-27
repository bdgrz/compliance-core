using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>Assigns an open request to an active client member with a due date at its owner revision.</summary>
[Discriminator("bdgrz.auditor-requests.assign-auditor-request", 2)]
public sealed record AssignAuditorRequest(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    Uuid OwnerMemberId, DateOnly DueOn, string AssignmentReason, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
