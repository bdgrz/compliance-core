using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the named change examination auditor decision for one examination.</summary>
[Discriminator("bdgrz.examinations.change-examination-auditor", 1)]
public sealed record ChangeExaminationAuditor(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    long ExpectedRevision, ExaminationAuditor Auditor)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    string IProgramScopedRequest.RequiredPermission => "examinations.manage";
}
