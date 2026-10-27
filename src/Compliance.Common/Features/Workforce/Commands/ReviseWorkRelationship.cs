using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Replaces a work relationship's terms; the person and source worker ID never change.</summary>
[Discriminator("bdgrz.workforce.work-relationship.revise", 1)]
public sealed record ReviseWorkRelationship(Uuid TenantId, Uuid RelationshipId,
    long ExpectedRevision, string WorkerType, string LifecycleStatus, DateOnly StartDate,
    DateOnly? EndDate = null, string? Department = null, Uuid? ManagerPersonId = null,
    Uuid? SponsorPersonId = null, string? EmploymentStatusReason = null)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
