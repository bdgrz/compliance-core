using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads one entry with its published-cut and resolution status.</summary>
[Discriminator("bdgrz.criteria.runtime.entry.get", 1)]
public sealed record GetRuntimeCriteriaEntry(Uuid TenantId, Uuid EditionId, string Identifier)
    : IRequest<RuntimeCriteriaEntry>, ITenantAccessRequest, ICallable;
