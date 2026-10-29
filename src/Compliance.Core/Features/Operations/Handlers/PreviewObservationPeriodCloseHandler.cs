using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AuditPopulations.Queries;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

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
