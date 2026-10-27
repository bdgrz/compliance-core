using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>Bounded identity, revision and current correlation; private values remain verified immutable content.</summary>
public sealed record PersonHeader(Uuid TenantId, Uuid PersonId, long Revision, Uuid? CorrelatedUserId, string ValuesSha256,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt);
