using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records management's review of one exact examination source chain.</summary>
[Discriminator("bdgrz.examinations.record-management-examination-review", 2)]
public sealed record RecordManagementExaminationReview(Uuid TenantId, Uuid ProgramId,
    Uuid ExaminationId, Uuid SignOffId, ExaminationSignOffContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
