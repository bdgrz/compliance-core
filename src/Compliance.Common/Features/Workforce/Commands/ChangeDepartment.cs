using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.department.change", 1)]
public sealed record ChangeDepartment(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    string? Department)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
