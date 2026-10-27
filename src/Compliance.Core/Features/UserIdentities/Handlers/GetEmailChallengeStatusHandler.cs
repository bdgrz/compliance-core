using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Reads the status of the current verification challenge: the address says whether it is verified, issued or
///     expired, and the delivery process's ledger says whether the message was delivered, is pending or failed.
/// </summary>
public sealed class GetEmailChallengeStatusHandler(IAggregateReader reader, IEmailChallengeDeliveryLedger deliveries,
    TimeProvider clock) : IRequestHandler<GetEmailChallengeStatus, EmailChallengeStatusView>
{
    public async ValueTask<Result<EmailChallengeStatusView>> HandleAsync(
        IRequestContext<GetEmailChallengeStatus> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result<EmailChallengeStatusView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var address = await reader.HydrateAsync(new EmailAddress(normalized), ct).ConfigureAwait(false);
        if (address.Owner != context.Request.UserId)
            return Result<EmailChallengeStatusView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The email address was not found."));
        var delivery = address.CurrentChallengeId is { } challengeId
            ? await deliveries.ReadAsync(challengeId, ct).ConfigureAwait(false)
            : null;
        return Result<EmailChallengeStatusView>.Success(address.GetChallengeStatus(clock.GetUtcNow(),
            delivery?.Status ?? "pending"));
    }
}
