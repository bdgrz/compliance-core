using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads complete scalar command authority; immutable body availability does not erase an accepted lifecycle decision.</summary>
static class ObservationPeriodRead
{
    public static async ValueTask<Result<ObservationPeriodView>> GetAsync(IAggregateReader reader, Uuid tenantId,
        Uuid programId, Uuid periodId, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new TypeIIPeriod(tenantId, programId, periodId), ct).ConfigureAwait(false);
        return owner.Header is { } header && header.TenantId == tenantId && header.ProgramId == programId && header.PeriodId == periodId
            ? Result<ObservationPeriodView>.Success(new ObservationPeriodView(tenantId, programId, periodId, header.ExaminationId,
                header.TypeIIPlanId, header.TypeIIPlanVersion, header.Status, header.Revision, header.PeriodStart, header.PeriodEnd,
                header.ExpectedOccurrenceCount, owner.Pending is TypeIIPeriodAmendmentAccepted ? owner.PendingOperationId : null,
                header.ValidationStatus, header.LatestCloseSnapshotId, header.LatestCloseVersion, header.UpdatedAt))
            : Result<ObservationPeriodView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The observation period has not opened in this program."));
    }
}
