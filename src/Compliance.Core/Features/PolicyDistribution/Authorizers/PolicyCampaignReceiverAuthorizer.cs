using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Authorizers;

/// <summary>Only the trusted process worker may dispatch participant and campaign closure receivers.</summary>
sealed class PolicyCampaignReceiverAuthorizer : IRequestAuthorizer<IPolicyCampaignReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IPolicyCampaignReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal campaign processes may dispatch participant and closure receivers.")));
}
