using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Replaces a non-human identity's terms, including its owner, review date, and
///     optional expiry date.</summary>
[Discriminator("bdgrz.workforce.service-identity.revise", 1)]
public sealed record ReviseServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId,
    long ExpectedRevision, string DisplayName, string IdentityKind, string Purpose,
    string OwnerKind, Uuid OwnerId, DateOnly ReviewBy, string LifecycleStatus,
    string? Environment = null, DateOnly? ExpiresOn = null) : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
