using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the named confirm examination dates decision for one examination.</summary>
[Discriminator("bdgrz.examinations.confirm-examination-dates", 1)]
public sealed record ConfirmExaminationDates(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    long ExpectedRevision, ExaminationDates Dates, string EvidenceReference)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
{
    string IProgramScopedRequest.RequiredPermission => "examinations.manage";
}
