using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads one bounded page of a published edition's source disclosures.</summary>
[Discriminator("bdgrz.criteria.runtime.disclosures.list", 1)]
public sealed record ListRuntimeCriteriaDisclosures(Uuid TenantId, Uuid EditionId,
    int? Limit = null, string? Cursor = null)
    : IRequest<RuntimeCriteriaDisclosurePage>, ITenantAccessRequest, ICallable;
