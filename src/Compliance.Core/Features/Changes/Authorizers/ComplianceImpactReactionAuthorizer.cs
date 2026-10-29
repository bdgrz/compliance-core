using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Incidents.Commands;
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
