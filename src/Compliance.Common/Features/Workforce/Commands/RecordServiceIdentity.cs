using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>
///     Records an active non-human identity with exactly one accountable owner (a recorded person
///     or an active team), an approved purpose, and a review date at most one year out (M0-D06).
///     An optional <c>ExpiresOn</c> must be in the future. The request ID
///     becomes the service identity ID.
/// </summary>
[Discriminator("bdgrz.workforce.service-identity.record", 1)]
public sealed record RecordServiceIdentity(Uuid TenantId, string DisplayName, string IdentityKind,
    string Purpose, string OwnerKind, Uuid OwnerId, DateOnly ReviewBy, string? Environment = null,
    DateOnly? ExpiresOn = null)
    : IRequest<ServiceIdentityRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
