using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

public sealed class GetTypeIEntryDecisionHandler(TypeIEntryDecisionReader decisions)
    : IRequestHandler<GetTypeIEntryDecision, TypeIEntryDecisionRecord>
{
    public async ValueTask<Result<TypeIEntryDecisionRecord>> HandleAsync(
        IRequestContext<GetTypeIEntryDecision> context, CancellationToken ct) =>
        await decisions.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.DecisionId, ct).ConfigureAwait(false) is { } view
            ? Result<TypeIEntryDecisionRecord>.Success(view)
            : Result<TypeIEntryDecisionRecord>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The Type I entry decision was not found."));
}
