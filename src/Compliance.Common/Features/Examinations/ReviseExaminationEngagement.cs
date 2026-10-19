using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the revise examination engagement intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.revise-examination-engagement", 1)]
public sealed record ReviseExaminationEngagement(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, long ExpectedRevision, string AuditFirm, DateOnly PeriodStart, DateOnly? PeriodEnd, IReadOnlyList<string> TrustServicesCategories, IReadOnlyList<Uuid>? ManagementContactMemberIds = null) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
