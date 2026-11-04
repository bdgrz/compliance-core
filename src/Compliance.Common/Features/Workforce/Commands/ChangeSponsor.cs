using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.sponsor.change", 1)]
public sealed record ChangeSponsor(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    Uuid SponsorPersonId)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
