using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Requires current source-backed operator authority before reading an exact value receipt.</summary>
public sealed class GetCriteriaEditionValueOperationHandler(ICriteriaEditionValueOperationReader reader,
    IAccessAdmissionAuthority? authority = null) : IRequestHandler<GetCriteriaEditionValueOperation, CriteriaOperationReceipt>
{
    public async ValueTask<Result<CriteriaOperationReceipt>> HandleAsync(
        IRequestContext<GetCriteriaEditionValueOperation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty ||
            !CriteriaEditionValueRules.Target(request.TargetKind, request.Field, request.Category, request.Code))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Validation,
                "The exact edition, target and operation are required."));
        var target = new CriteriaEditionValue(request.EditionId, request.TargetKind, request.Field, request.Category, request.Code);
        var authorization = await CriteriaOperatorReadAdmission.AuthorizeAsync(authority, context, request.EditionId,
            request.OperationId, target.Stream, "edition-value-operation", ct).ConfigureAwait(false);
        if (!authorization.IsSuccess)
            return Result<CriteriaOperationReceipt>.Failure(authorization.Error!);
        return await reader.ReadAsync(request.EditionId, request.TargetKind, request.Field, request.Category,
            request.Code, request.OperationId, ct).ConfigureAwait(false);
    }
}
