using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

/// <summary>Delivery outcomes are recorded by the trusted process principal only; no actor decides what the relay did.</summary>
public sealed class EmailChallengeDeliveryOutcomeAuthorizer : IRequestAuthorizer<IEmailChallengeDeliveryOutcome>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEmailChallengeDeliveryOutcome> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the email challenge delivery process may record delivery outcomes.")));
}
