using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Records the create examination engagement intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.create-examination-engagement", 1)]
public sealed record CreateExaminationEngagement(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, string EngagementType, string AuditFirm, DateOnly PeriodStart, DateOnly? PeriodEnd, IReadOnlyList<string> TrustServicesCategories, Uuid BoundaryId, IReadOnlyList<Uuid>? ManagementContactMemberIds = null) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ExaminationId;
}
