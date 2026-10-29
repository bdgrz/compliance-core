using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Records that the relay accepted one delivery attempt. An outcome of an attempt that a newer attempt replaced is
///     ignored, an exact repeat changes nothing, and a different outcome for the same attempt conflicts.
/// </summary>
public sealed class RecordEmailChallengeDeliverySentHandler(IEmailChallengeDeliveryLedger ledger)
    : IRequestHandler<RecordEmailChallengeDeliverySent>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordEmailChallengeDeliverySent> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ChallengeId == Uuid.Empty || request.AttemptId == Uuid.Empty ||
            request.SentAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A delivery outcome requires its command, challenge, attempt and time."));
        var decision = await ledger.RecordOutcomeAsync(request.ChallengeId, request.AttemptId,
            EmailChallengeDeliveryAttempt.Sent, null, request.CommandId, request.SentAt, ct).ConfigureAwait(false);
        return EmailChallengeDeliveryOutcomes.ToResult(decision);
    }
}
