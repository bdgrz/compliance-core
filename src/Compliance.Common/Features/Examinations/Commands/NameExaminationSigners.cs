using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the named name examination signers decision for one examination.</summary>
[Discriminator("bdgrz.examinations.name-examination-signers", 1)]
public sealed record NameExaminationSigners(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    long ExpectedRevision, Uuid CeoSignerMemberId, Uuid CisoSignerMemberId)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    string IProgramScopedRequest.RequiredPermission => "tenant.rbac.manage";
}
