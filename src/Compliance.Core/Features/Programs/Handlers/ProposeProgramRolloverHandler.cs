using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

/// <summary>Proposes carry-forward choices against a closed Type II period and its auditor outcome.</summary>
public sealed class ProposeProgramRolloverHandler(IAggregateExecutor executor,
    IAggregateReader reader, TypeIIPeriodReader periods, TimeProvider clock) : IRequestHandler<ProposeProgramRollover>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ProposeProgramRollover> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A program rollover proposal requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.RolloverId == Uuid.Empty || request.Content is null ||
            request.Content.SourcePeriodId == Uuid.Empty || request.Content.AuditOutcomeId == Uuid.Empty)
            return Invalid("A rollover proposal requires exact completed-period and auditor-outcome identities.");

        var periodRead = await periods.TryReadAsync(request.TenantId, request.ProgramId, request.Content.SourcePeriodId, ct).ConfigureAwait(false);
        if (!periodRead.IsSuccess)
            return Result.Failure(periodRead.Error!);
        var sourcePeriod = periodRead.Value;
        if (sourcePeriod.View is not { } periodView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The source Type II period was not found in this program."));
        if (periodView.Status != "closed" || periodView.CloseSnapshots.Count == 0)
            return Conflict("A rollover can be proposed only from a frozen, closed Type II period.");

        var outcome = await reader.HydrateAsync(new AuditOutcome(request.TenantId,
            request.ProgramId, request.Content.AuditOutcomeId), ct).ConfigureAwait(false);
        if (outcome.Current is not { } outcomeView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The auditor outcome was not found in this program."));

        var now = clock.GetUtcNow();
        var latestSnapshot = periodView.CloseSnapshots[^1];
        if (now == default || now < latestSnapshot.RecordedAt || now < outcomeView.RecordedAt ||
            outcomeView.RecordedAt < latestSnapshot.RecordedAt ||
            outcomeView.Content.IssuedOn > DateOnly.FromDateTime(now.UtcDateTime))
            return Conflict("A rollover requires a completed close and an auditor outcome recorded before the proposal.");

        return await executor.ExecuteAsync(new ProgramRollover(request.TenantId,
                request.ProgramId, request.RolloverId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Propose(request.Content,
                    periodView, outcomeView, actorMemberId, actorDisplay, now)), context, ct)
            .ConfigureAwait(false);
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
