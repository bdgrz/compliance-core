using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Source-backed read-only close readiness; publishing immutable candidate bytes never admits or changes the period.</summary>
public sealed class PreviewObservationPeriodCloseHandler(IAggregateReader reader, TypeIIPeriodReader periods,
    ITypeIIPeriodCloseReconciliation sources, TimeProvider clock)
    : IRequestHandler<PreviewObservationPeriodClose, ObservationPeriodClosePreview>
{
    public async ValueTask<Result<ObservationPeriodClosePreview>> HandleAsync(IRequestContext<PreviewObservationPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<ObservationPeriodClosePreview>.Failure(new RequestError(RequestErrorKind.Forbidden, "A close preview requires a current personal program reader."));
        var read = await periods.TryReadAsync(request.TenantId, request.ProgramId, request.ObservationPeriodId, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<ObservationPeriodClosePreview>.Failure(read.Error!);
        var period = read.Value;
        if (period.View is not { } state)
            return Result<ObservationPeriodClosePreview>.Failure(new RequestError(RequestErrorKind.NotFound, "The observation period has not opened in this program."));
        var at = clock.GetUtcNow();
        var operation = RootOperation.Provenance(Uuid.CreateVersion4(), nameof(PreviewObservationPeriodClose), period.Stream,
            ActorReference.ForMember(RbacIds.Member(request.TenantId, user), UserIdentityClaims.BdgrzDisplay(context.Actor, user)),
            period.Revision, AuthoredIntentDigest.Compute(request), at);
        try
        {
            var source = await sources.ReadAsync(period, context.Actor, user, operation, ct).ConfigureAwait(false);
            if (!source.IsSuccess)
                return Result<ObservationPeriodClosePreview>.Failure(source.Error!);
            var current = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId, request.ProgramId, request.ObservationPeriodId), ct).ConfigureAwait(false);
            if (current.NativeSequence != period.Owner.NativeSequence || current.CommittedStreamPosition != period.Owner.CommittedStreamPosition)
                return Conflict("The period changed during its close preview. Retry the query.", true);
            var value = source.Value;
            if (value.RecordedThrough != at || !value.ExpectedOccurrenceIds.OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .SequenceEqual(state.ExpectedOccurrences.Select(item => item.OccurrenceId).OrderBy(id => id.ToString(), StringComparer.Ordinal)))
                return Conflict("The close preview source differs from its exact expected population or server receipt cut.");
            var blockers = value.SourceBlockers.ToList();
            if (period.PendingOperationId is not null)
                blockers.Add("An opening or amendment is still being applied.");
            if (DateOnly.FromDateTime(at.UtcDateTime) <= state.PeriodEnd)
                blockers.Add("The complete observation interval has not ended.");
            var ready = blockers.Count == 0 && value.MissingWorkIds.Count == 0 && value.DuplicateOccurrenceIds.Count == 0 &&
                value.ExpectedOccurrenceIds.OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .SequenceEqual(value.ActualOccurrenceIds.OrderBy(id => id.ToString(), StringComparer.Ordinal));
            return Result<ObservationPeriodClosePreview>.Success(new ObservationPeriodClosePreview(request.TenantId, request.ProgramId,
                request.ObservationPeriodId, period.Revision, ready, value.ExpectedOccurrenceIds.Count, value.ActualOccurrenceIds.Count,
                value.MissingWorkIds, value.DuplicateOccurrenceIds, value.UnresolvedMaterialItemIds, blockers, value.SourceDifferences, at));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Conflict("The exact close preview source content cannot be verified.", exception is ImmutableContentUnavailableException);
        }
    }
    static Result<ObservationPeriodClosePreview> Conflict(string message, bool transient = false) =>
        Result<ObservationPeriodClosePreview>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
