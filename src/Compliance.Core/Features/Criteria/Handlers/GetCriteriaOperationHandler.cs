using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads a source-backed outcome without inferring refusal from absence.</summary>
public sealed class GetCriteriaOperationHandler(ICriteriaOperationReader reader,
    IAccessAdmissionAuthority? authority = null)
    : IRequestHandler<GetCriteriaOperation, CriteriaOperationReceipt>
{
    public async ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(
        IRequestContext<GetCriteriaOperation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty ||
            request.Identifier is not null && !CriteriaContentBounds.Identifier(request.Identifier))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Validation,
                "An edition, operation and valid entry identity are required."));
        var target = request.Identifier is null ? CriteriaEdition.AddressOf(request.EditionId) :
            CriteriaEntry.StreamFor(request.EditionId, request.Identifier);
        var authorization = await CriteriaOperatorReadAdmission.AuthorizeAsync(authority, context,
            request.EditionId, request.OperationId, target, "operation", ct).ConfigureAwait(false);
        return authorization.IsSuccess ? await reader.GetOperationAsync(request.EditionId, request.OperationId,
            request.Identifier, ct).ConfigureAwait(false) : Result<CriteriaOperationReceipt>.Failure(authorization.Error!);
    }
}
