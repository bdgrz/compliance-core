using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the named open examination decision for one examination.</summary>
[Discriminator("bdgrz.examinations.open-examination", 1)]
public sealed record OpenExamination(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    string ExaminationType, ExaminationAuditor Auditor)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    string IProgramScopedRequest.RequiredPermission => "examinations.manage";
}
