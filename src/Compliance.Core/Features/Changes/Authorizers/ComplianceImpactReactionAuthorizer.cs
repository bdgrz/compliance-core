using Bdgrz.Compliance.Features.Changes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Authorizers;

sealed class ComplianceImpactReactionAuthorizer : IRequestAuthorizer<IComplianceImpactReactionRequest>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<IComplianceImpactReactionRequest> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a trusted reactor may dispatch compliance-impact follow-up.")));
}
