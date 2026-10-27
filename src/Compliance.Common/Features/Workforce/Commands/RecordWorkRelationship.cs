using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>
///     Records a person's work relationship under a stable source worker ID. The worker ID is
///     unique per tenant: recording it again for another person conflicts.
/// </summary>
[Discriminator("bdgrz.workforce.work-relationship.record", 1)]
public sealed record RecordWorkRelationship(Uuid TenantId, Uuid PersonId, string SourceWorkerId,
    string WorkerType, string LifecycleStatus, DateOnly StartDate, DateOnly? EndDate = null,
    string? Department = null, Uuid? ManagerPersonId = null, Uuid? SponsorPersonId = null,
    string? EmploymentStatusReason = null)
    : IRequest<WorkRelationshipRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
