using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.service-identity.review", 1)]
public sealed record ReviewServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId, long ExpectedRevision, DateOnly NextReviewDate, string? Notes = null)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest;
