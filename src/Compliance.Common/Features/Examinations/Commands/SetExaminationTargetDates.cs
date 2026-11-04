using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the named set examination target dates decision for one examination.</summary>
[Discriminator("bdgrz.examinations.set-examination-target-dates", 1)]
public sealed record SetExaminationTargetDates(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    long ExpectedRevision, ExaminationDates Dates)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    string IProgramScopedRequest.RequiredPermission => "examinations.manage";
}
