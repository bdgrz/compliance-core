using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
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
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Authorizers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

public sealed class RecordArtifactDispositionResultHandler(IAggregateExecutor executor,
    IDomainEventReader events)
    : IRequestHandler<RecordArtifactDispositionResult>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordArtifactDispositionResult> context, CancellationToken ct)
    {
        if (!RecordArtifactDispositionResultAuthorizer.IsNamedWorker(context))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named artifact disposition worker may record storage results."));

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.DispositionId == Uuid.Empty ||
            request.ArtifactId == Uuid.Empty || request.ExpectedRevision <= 0 ||
            request.AttemptNumber <= 0 || request.ExecutionRequestEventId == Uuid.Empty ||
            request.CompletedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An artifact disposition result requires its exact execution attempt and completion time."));
        if (context.CausationId != request.ExecutionRequestEventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The result must be caused by the exact artifact disposition execution request."));
        if (!await IsExactExecutionRequestAsync(request, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The execution request event does not match this exact artifact disposition attempt."));

        var actor = ActorReference.ForSystemProcess(
            RecordArtifactDispositionResultAuthorizer.ProcessId, "Artifact disposition worker");
        return await executor.ExecuteAsync(new ArtifactDisposition(request.TenantId,
                request.DispositionId), disposition =>
            {
                var current = disposition.Current;
                if (current is null || current.ArtifactId != request.ArtifactId)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.NotFound, "The artifact disposition was not found.")));
                if (current.Attempts.Count == 0 ||
                    current.Attempts[^1].Number != request.AttemptNumber)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The execution result belongs to an earlier disposition attempt.")));

                return AggregateOutcome.CommitOnSuccess(disposition.RecordExecutionResult(
                    request.ExpectedRevision, request.Outcome, request.FailureCode, actor,
                    request.CompletedAt));
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> IsExactExecutionRequestAsync(RecordArtifactDispositionResult request,
        CancellationToken ct)
    {
        var stream = new ArtifactDisposition(request.TenantId, request.DispositionId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != request.ExecutionRequestEventId)
                continue;
            return record.Stream == stream &&
                record.Event is ArtifactDispositionExecutionRequested execution &&
                execution.TenantId == request.TenantId &&
                execution.SubjectId == request.DispositionId &&
                execution.Revision == request.ExpectedRevision &&
                execution.State is { } state && state.TenantId == request.TenantId &&
                state.DispositionId == request.DispositionId && state.ArtifactId == request.ArtifactId &&
                state.Attempts is { Count: > 0 } &&
                state.Attempts[^1].Number == request.AttemptNumber &&
                state.Attempts[^1].Status == "requested";
        }
        return false;
    }
}
