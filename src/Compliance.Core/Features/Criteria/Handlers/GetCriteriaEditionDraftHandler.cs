using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads the source-backed state of a criteria edition draft.</summary>
public sealed class GetCriteriaEditionDraftHandler(ICriteriaOperationReader reader,
    IAccessAdmissionAuthority? authority = null)
    : IRequestHandler<GetCriteriaEditionDraft, CriteriaEditionDraftView>
{
    public async ValueTask<Result<CriteriaEditionDraftView>> HandleAsync(
        IRequestContext<GetCriteriaEditionDraft> context, CancellationToken ct)
    {
        var editionId = context.Request.EditionId;
        if (editionId == Uuid.Empty)
            return Result<CriteriaEditionDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An edition identity is required."));
        var operation = AggregateIdentity.Create("CriteriaEditionDraftRead", editionId);
        var authorization = await CriteriaOperatorReadAdmission.AuthorizeAsync(authority, context,
            editionId, operation, CriteriaEdition.AddressOf(editionId), "edition-draft", ct).ConfigureAwait(false);
        return authorization.IsSuccess ? await reader.GetDraftAsync(editionId, ct).ConfigureAwait(false) :
            Result<CriteriaEditionDraftView>.Failure(authorization.Error!);
    }
}
