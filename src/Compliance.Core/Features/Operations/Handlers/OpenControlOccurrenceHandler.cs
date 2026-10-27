using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>
///     Opens an event-driven or ad hoc occurrence for its holder or a program manager. The occurrence identity
///     derives from the business window only: a trigger or date never creates a second occurrence for one window, and
///     the command id distinguishes authored attempts.
/// </summary>
public sealed class OpenControlOccurrenceHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, ControlReader controlSources, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<OpenControlOccurrence, ControlOccurrenceView>
{
    const string CommandKind = "OpenControlOccurrence";

    public ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<OpenControlOccurrence> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOccurrenceView>> HandleCoreAsync(
        IRequestContext<OpenControlOccurrence> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0 || request.BusinessWindow is null ||
            !request.BusinessWindow.IsValid)
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An occurrence opening requires a command id, a valid business window and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var occurrenceId = ControlOccurrenceIdentity.Of(request.TenantId, request.ProgramId, request.ControlId,
            request.BusinessWindow);
        var stream = new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId, occurrenceId)
            .Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, CommandKind, stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ControlOccurrenceTerms? terms = null;
        ImmutableContentRef? creationIntent = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOccurrence>(binding,
            () => new ControlOccurrence(request.TenantId, request.ProgramId, request.ControlId, occurrenceId,
                request.CommandId), occurrence => occurrence.Acceptance,
            async (current, token) =>
            {
                try
                {
                    var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId, request.ProgramId,
                        request.ControlId, token).ConfigureAwait(false);
                    if (control is null)
                        return Result<FirstResponseDescriptor>.Failure(ControlOperationsSource.ControlNotFound());
                    var snapshot = await operatingReader.ReadControlAsync(request.TenantId, request.ProgramId, request.ControlId,
                        token).ConfigureAwait(false);
                    var plan = snapshot.PlanOn(request.ControlId, request.OccurredOn);
                    if (plan is not null &&
                        !await authority.HoldsAsync(request.TenantId, plan.Owner, actor.MemberId, token).ConfigureAwait(false) &&
                        !await authority.HoldsAsync(request.TenantId, plan.BackupOwner, actor.MemberId, token).ConfigureAwait(false) &&
                        !await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, token).ConfigureAwait(false))
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                            "Only an operating holder or a program manager may open an occurrence."));

                    var failure = ControlOperationRules.CheckOpening(plan, OperatingAuthority.VersionWindows(control),
                        request.Trigger, request.OccurredOn, request.BusinessWindow, now);
                    if (failure is not null)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    var prepared = current.PrepareOpen(request.ExpectedRevision);
                    if (!prepared.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
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
                        ControlOccurrenceCreationIntent.Schema, intent, token).ConfigureAwait(false);
                    var body = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                        ControlOperatingContent.OccurrenceViewSchema, view, token).ConfigureAwait(false);
                    result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                        ControlOperatingContent.OccurrenceViewSchema, body, token).ConfigureAwait(false);
                    response = ControlOperatingContent.OkResponse(ControlOperatingContent.OccurrenceViewSchema, body);
                    return Result<FirstResponseDescriptor>.Success(response!);
                }
                catch (ImmutableContentIntegrityException failure)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact operating source content cannot be verified.", failure is ImmutableContentUnavailableException));
                }
            },
            (ControlOccurrence occurrence, ImmutableContentRef frozen) => occurrence.Open(provenance, terms!,
                creationIntent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOccurrenceView>(content, request.TenantId, outcome,
            ct).ConfigureAwait(false);
    }

}
