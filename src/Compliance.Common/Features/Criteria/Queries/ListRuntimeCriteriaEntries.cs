using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads a stable, source-cut-bound page of independently owned entries.</summary>
[Discriminator("bdgrz.criteria.runtime.entries.list", 1)]
public sealed record ListRuntimeCriteriaEntries(Uuid TenantId, Uuid EditionId, string? Category = null,
    string? Kind = null, string? ParentIdentifier = null, int? Limit = null, string? Cursor = null)
    : IRequest<RuntimeCriteriaPage>, ITenantAccessRequest, ICallable;
