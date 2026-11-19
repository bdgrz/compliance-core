using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads one runtime edition and its source-coverage status.</summary>
[Discriminator("bdgrz.criteria.runtime.edition.get", 1)]
public sealed record GetRuntimeCriteriaEdition(Uuid TenantId, Uuid EditionId)
    : IRequest<RuntimeCriteriaEdition>, ITenantAccessRequest, ICallable;
