using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>
///     Admits a period range amendment. The old range, its exact prior calendar grant, the new range and the complete
///     attributed amendment are frozen on the period owner before the calendar is touched, so the calendar holds both
///     ranges until the period applies the amendment. An exact retry replays the original acknowledgment.
/// </summary>
public sealed class AmendTypeIIObservationPeriodHandler(NativeOperationExecutor operations, IAggregateReader reader, TypeIIPeriodReader periods,
    IImmutableContentStore content, TimeProvider clock, TypeIICalendarReader calendars)
    : IRequestHandler<AmendTypeIIObservationPeriod, OperationAcceptedResponse>
{
    const string CommandKind = "AmendTypeIIObservationPeriod";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<AmendTypeIIObservationPeriod> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Failure(RequestErrorKind.Forbidden, "A Type II period amendment requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PeriodId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A command id, tenant, program and period are required.");
        if (request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation, "The expected revision cannot be negative.");
        if (request.PeriodStart is null && request.PeriodEnd is null)
            return Failure(RequestErrorKind.Validation, "A Type II period amendment must change at least one boundary.");

        var realm = request.TenantId.ToString();
        var now = clock.GetUtcNow();
        var snapshot = new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId);
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind,
            ActorReference.ForMember(actorMemberId, actorDisplay), snapshot.Stream, InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, snapshot.Stream,
            snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        var sequence = 0L;
        var position = 0UL;
        var outcome = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(binding,
            () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.CommandId),
            period => period.Acceptance,
            async (period, token) =>
            {
                try
                {
                    if (period.ProgramId != request.ProgramId)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The period was not found in this program."));
                    var read = await periods.ReadAsync(period, token).ConfigureAwait(false);
                    var frozen = await FreezeAsync(request, read, provenance, actorMemberId, actorDisplay, now, token).ConfigureAwait(false);
                    if (!frozen.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                    intent = frozen.Value.Intent;
                    sequence = period.NativeSequence;
                    position = period.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The period's exact immutable preparation cannot be verified.", exception is ImmutableContentUnavailableException));
                }
            }, (TypeIIPeriod period, ImmutableContentRef acknowledgment) =>
                period.AtCut(sequence, position) is { IsSuccess: false } changed ? changed :
                period.AcceptAmendment(request.ExpectedRevision, provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await TypeIIRangeContent.AcknowledgmentAsync(content, realm, outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response,
        TypeIIPeriodCalendarAuthorization Held)>> FreezeAsync(AmendTypeIIObservationPeriod request,
        TypeIIPeriodReadSnapshot period, OperationProvenance provenance, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset now, CancellationToken ct)
    {
        if (period.View is not { } current)
            return Fail(RequestErrorKind.NotFound, "The Type II observation period was not found.");
        if (period.Owner.PendingOperationId is not null)
            return Fail(RequestErrorKind.Conflict,
                "Another opening or amendment is still being applied; retry after it ends.", true);
        var start = request.PeriodStart ?? current.PeriodStart;
        var end = request.PeriodEnd ?? current.PeriodEnd;
        var reason = request.AmendmentReason?.Trim() ?? string.Empty;
        var failure = period.Rules.PrepareAmendment(request.ExpectedRevision, start, end, reason, actorMemberId,
            actorDisplay, now, out _);
        if (failure is not null)
            return Result<(ImmutableContentRef, FirstResponseDescriptor, TypeIIPeriodCalendarAuthorization)>.Failure(
                failure.ToRequestError());
        if (period.Owner.CalendarAuthorization is not { } held)
            return Fail(RequestErrorKind.Conflict, "The period holds no live calendar range to amend.");

        var calendarOwner = await reader.HydrateAsync(new TypeIIObservationCalendar(request.TenantId, request.ProgramId),
            ct).ConfigureAwait(false);
        var calendar = await calendars.ReadAsync(calendarOwner, [held.AuthorizationId], ct).ConfigureAwait(false);
        if (calendar.Find(held.AuthorizationId) is not { IsLive: true } reservation ||
            reservation.PeriodId != request.PeriodId || reservation.AuthorizedFact is not { } grantFact)
            return Fail(RequestErrorKind.Conflict, "The calendar range of the period is not visible yet.", true);
        var oldRange = new ObservationRange(current.PeriodStart, current.PeriodEnd);
        if (reservation.Generation != held.Generation || reservation.Range != oldRange)
            return Fail(RequestErrorKind.Conflict, "The period and the program calendar disagree about the held range.");

        var realm = request.TenantId.ToString();
        var prior = new ReservationGrant(TypeIIRangeEffects.KeyOf(held.AuthorizationId, period.Owner.Stream, calendar.Stream),
            (ulong)reservation.Generation, NativeFactReference.Of(calendar.Stream, grantFact));
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), TypeIIPeriodAmendmentChange.Schema,
            new TypeIIPeriodAmendmentChange(start, end, reason, actorMemberId, actorDisplay.Trim(), now), ct)
            .ConfigureAwait(false);
        var header = new DurableIntentHeader(request.CommandId, period.Owner.Stream, period.Owner.Stream, period.Owner.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256,
            now);
        var intent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), TypeIIRangeIntent.Schema,
            new TypeIIRangeIntent(header, request.ProgramId, request.PeriodId, TypeIIRangeOperationKind.Amendment,
                prior, oldRange, new ObservationRange(start, end), change,
                TypeIIRangeEffects.KeyOf(request.CommandId, period.Owner.Stream, calendar.Stream)), ct)
            .ConfigureAwait(false);
        var accepted = await TypeIIRangeContent.AcceptedResponseAsync(content, realm, request.CommandId, now, ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor, TypeIIPeriodCalendarAuthorization)>.Success(
            (intent, accepted, held));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor, TypeIIPeriodCalendarAuthorization)> Fail(
        RequestErrorKind kind, string message, bool transient = false) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor, TypeIIPeriodCalendarAuthorization)>.Failure(
            new RequestError(kind, message, transient));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
