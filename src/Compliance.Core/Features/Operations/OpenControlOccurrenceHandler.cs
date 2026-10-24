using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Opens an event-driven or ad hoc occurrence for its holder or a program manager. The occurrence identity
///     derives from the business window only: a trigger or date never creates a second occurrence for one window, and
///     the command id distinguishes authored attempts.
/// </summary>
public sealed class OpenControlOccurrenceHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<OpenControlOccurrence, ControlOccurrenceView>
{
    const string CommandKind = "OpenControlOccurrence";

    public async ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<OpenControlOccurrence> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0 || request.BusinessWindow is null ||
            !request.BusinessWindow.IsValid)
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An occurrence opening requires a command id, a valid business window and a non-negative expected revision."));
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOccurrenceView>.Failure(ControlOperationsSource.ControlNotFound());
        var snapshot = await operatingReader.ReadControlAsync(request.TenantId, request.ProgramId, request.ControlId,
            ct).ConfigureAwait(false);
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var plan = snapshot.PlanOn(request.ControlId, request.OccurredOn);
        if (plan is not null &&
            !await authority.HoldsAsync(request.TenantId, plan.Owner, actor.MemberId, ct).ConfigureAwait(false) &&
            !await authority.HoldsAsync(request.TenantId, plan.BackupOwner, actor.MemberId, ct).ConfigureAwait(false) &&
            !await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only an operating holder or a program manager may open an occurrence."));

        var now = clock.GetUtcNow();
        var occurrenceId = ControlOccurrenceIdentity.Of(request.TenantId, request.ProgramId, request.ControlId,
            request.BusinessWindow);
        var stream = new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId, occurrenceId)
            .Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, CommandKind, stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var (current, _) = await operatingReader.ReadStateAsync(request.TenantId, request.ProgramId, request.ControlId,
            occurrenceId, request.CommandId, ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ControlOccurrenceTerms? terms = null;
        ImmutableContentRef? creationIntent = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var failure = ControlOperationRules.CheckOpening(plan, OperatingAuthority.VersionWindows(control),
                request.Trigger, request.OccurredOn, request.BusinessWindow, now);
            if (failure is not null)
                return Result<ControlOccurrenceView>.Failure(failure.ToRequestError());
            var prepared = current.PrepareOpen(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<ControlOccurrenceView>.Failure(prepared.Error!);
            var approval = snapshot.Line(request.ControlId).Approvals[plan!.PlanVersionId];
            var intent = new ControlOccurrenceCreationIntent(
                new DurableIntentHeader(provenance.RootOperationId, stream, stream, stream, provenance.Actor,
                    provenance.Episode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now),
                request.TenantId, request.ProgramId, request.ControlId, occurrenceId, request.BusinessWindow,
                plan.PlanVersionId, NativeFactReference.Of(new ControlOperatingPlan(request.TenantId,
                    request.ProgramId, request.ControlId).Stream, approval), plan.Owner, plan.BackupOwner,
                plan.ReviewerMemberId, request.Trigger.Trim(), now);
            terms = new ControlOccurrenceTerms(plan.Cadence.Kind, plan.ControlVersionId, plan.PlanVersionId,
                request.OccurredOn, request.OccurredOn, request.OccurredOn.AddDays(plan.Cadence.DueWithinDays ?? 0),
                request.Trigger.Trim(), plan.Owner, provenance.Actor, now, request.BusinessWindow);
            var view = ControlOccurrenceFold.Present(new ControlOccurrenceView(request.TenantId, request.ProgramId,
                request.ControlId, occurrenceId, 1, terms.Kind, ControlOperationStates.Open, terms.ControlVersionId,
                terms.PlanVersionId, terms.PeriodStart, terms.PeriodEnd, terms.DueOn, terms.Trigger, terms.Assignee,
                [], [], []), DateOnly.FromDateTime(now.UtcDateTime));
            creationIntent = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOccurrenceCreationIntent.Schema, intent, ct).ConfigureAwait(false);
            var body = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOperatingContent.OccurrenceViewSchema, view, ct).ConfigureAwait(false);
            result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                ControlOperatingContent.OccurrenceViewSchema, body, ct).ConfigureAwait(false);
            response = ControlOperatingContent.OkResponse(ControlOperatingContent.OccurrenceViewSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(binding,
            () => new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId, occurrenceId,
                request.CommandId), occurrence => occurrence.Acceptance,
            occurrence => Prepare(occurrence, request.ExpectedRevision, response),
            (ControlOccurrence occurrence, ImmutableContentRef frozen) => occurrence.Open(provenance, terms!,
                creationIntent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, request.TenantId, outcome,
            ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlOccurrence occurrence, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = occurrence.PrepareOpen(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The occurrence changed while the opening was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
