using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.work-relationship.dates.correct", 1)]
public sealed record CorrectWorkRelationshipDates(Uuid TenantId, Uuid RelationshipId, long ExpectedRevision,
    DateOnly StartDate, DateOnly? EndDate, string Reason)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
