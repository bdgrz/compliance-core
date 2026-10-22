using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Records a finding. A governed source must exist in this program: a readiness gap names its
///     assessment as the source version, and a control occurrence must have been recorded.
/// </summary>
public sealed class RaiseFindingHandler(IAggregateExecutor executor, IAggregateReader reader,
    IDomainEventReader events, TimeProvider clock) : IRequestHandler<RaiseFinding, FindingRegistration>
{
    public async ValueTask<Result<FindingRegistration>> HandleAsync(
        IRequestContext<RaiseFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var selected = await reader.HydrateAsync(new FindingAggregate(request.TenantId, request.ProgramId,
            context.RequestId, context.RequestId), ct).ConfigureAwait(false);
        if (selected.HasSelectedReceipt)
        {
            var failure = selected.Raise(context.RequestId, request.Source, request.Title, request.Description,
                request.Severity, request.AffectedScope, request.OwnerMemberId, request.DueOn, request.Links ?? [],
                ActorReference.ForMember(actor.MemberId, actor.Display), now);
            return failure is null ? Result<FindingRegistration>.Success(new FindingRegistration(context.RequestId, 1))
                : Result<FindingRegistration>.Failure(failure.ToRequestError());
        }
        if (await SourceErrorAsync(request, ct).ConfigureAwait(false) is { } sourceError)
            return Result<FindingRegistration>.Failure(sourceError);
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                request.OwnerMemberId, ct).ConfigureAwait(false))
            return Result<FindingRegistration>.Failure(RemediationCommands.InactiveOwner());
        var ready = await FindingStreamRouting.EnsureReadyAsync(executor, reader, events, context,
            request.TenantId, request.ProgramId, now, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<FindingRegistration>.Failure(ready.Error);
        return await executor.ExecuteAsync(new FindingAggregate(request.TenantId,
                request.ProgramId, context.RequestId, context.RequestId),
            finding =>
            {
                var failure = finding.Raise(context.RequestId, request.Source, request.Title,
                    request.Description, request.Severity, request.AffectedScope,
                    request.OwnerMemberId, request.DueOn, request.Links ?? [],
                    ActorReference.ForMember(actor.MemberId, actor.Display), now);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    new FindingRegistration(context.RequestId, 1));
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<RequestError?> SourceErrorAsync(RaiseFinding request, CancellationToken ct)
    {
        var source = request.Source;
        if (source?.RecordId is not { } recordId)
            return null;
        var missing = new RequestError(RequestErrorKind.Validation,
            "The finding source record was not found in this program.");
        switch (source.Kind)
        {
            case "readiness_gap":
                if (!Uuid.TryParse(source.Version, null, out var assessmentId))
                    return missing;
                var readiness = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
                    request.ProgramId), ct).ConfigureAwait(false);
                return readiness.ReadGaps(assessmentId)?.Any(gap => gap.GapId == recordId) == true
                    ? null
                    : missing;
            case "control_occurrence" or "occurrence_review":
                var operations = await reader.HydrateAsync(new ControlOperationsLedger(
                    request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
                return operations.OccurrenceControlId(recordId) is null ? missing : null;
            default:
                return null;
        }
    }
}
