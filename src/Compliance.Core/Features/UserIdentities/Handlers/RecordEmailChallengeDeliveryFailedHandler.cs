using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Records that one delivery attempt did not end in an accepted message, with how much that proves. An outcome of an
///     attempt that a newer attempt replaced is ignored, an exact repeat changes nothing, and a different outcome for
///     the same attempt conflicts unless it settles an ambiguous latest attempt.
/// </summary>
public sealed class RecordEmailChallengeDeliveryFailedHandler(IEmailChallengeDeliveryLedger ledger)
    : IRequestHandler<RecordEmailChallengeDeliveryFailed>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordEmailChallengeDeliveryFailed> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ChallengeId == Uuid.Empty || request.AttemptId == Uuid.Empty ||
            request.FailedAt == default || !EmailChallengeFailureCertainty.IsDefined(request.Certainty) ||
            !EmailChallengeDeliveryOutcomes.IsFailureCode(request.FailureCode))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A delivery failure requires its command, challenge, attempt, time, certainty and a redacted code."));
        var decision = await ledger.RecordOutcomeAsync(request.ChallengeId, request.AttemptId, request.Certainty,
            request.FailureCode, request.CommandId, request.FailedAt, ct).ConfigureAwait(false);
        return EmailChallengeDeliveryOutcomes.ToResult(decision);
    }
}
