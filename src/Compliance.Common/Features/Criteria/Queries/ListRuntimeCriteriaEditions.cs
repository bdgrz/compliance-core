using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads a bounded page of published criteria editions.</summary>
[Discriminator("bdgrz.criteria.runtime.editions.list", 1)]
public sealed record ListRuntimeCriteriaEditions(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<RuntimeCriteriaEditionsPage>, ITenantAccessRequest, ICallable;
