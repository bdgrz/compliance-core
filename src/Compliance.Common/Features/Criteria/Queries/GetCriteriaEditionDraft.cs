using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Queries;

/// <summary>Reads one edition's draft, publication and label-transition state.</summary>
[Discriminator("bdgrz.criteria.edition.draft.get", 1)]
public sealed record GetCriteriaEditionDraft(Uuid EditionId)
    : IRequest<CriteriaEditionDraftView>, IPlatformOperatorRequest, ICallable;
