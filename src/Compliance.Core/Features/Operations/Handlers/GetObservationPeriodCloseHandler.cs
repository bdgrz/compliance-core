using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
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
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Resolves one unambiguous scoped historical close from native identity discovery and exact source-backed period lineage.</summary>
public sealed class GetObservationPeriodCloseHandler(IObservationPeriodCloseDirectoryReader directory,
    ObservationPeriodDirectoryReadConsistency consistency, TypeIIPeriodReader periods)
    : IRequestHandler<GetObservationPeriodClose, ObservationPeriodCloseView>
{
    public async ValueTask<Result<ObservationPeriodCloseView>> HandleAsync(IRequestContext<GetObservationPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ObservationPeriodCloseView>.Failure(fence.Error!);
        var identities = await directory.FindAsync(request.TenantId, request.ProgramId, request.ObservationPeriodCloseId, ct).ConfigureAwait(false);
        if (identities.Any(row => row.TenantId != request.TenantId || row.ProgramId != request.ProgramId || row.CloseId != request.ObservationPeriodCloseId))
            return Conflict("The close directory contains another tenant, program or close identity.");
        if (identities.Count > 1)
            return Conflict("The close identity is ambiguous across periods in this program.");
        Result<ObservationPeriodCloseView> result;
        if (identities.Count == 0)
            result = Result<ObservationPeriodCloseView>.Failure(new RequestError(RequestErrorKind.NotFound, "The frozen close was not found in this program."));
        else
        {
            var identity = identities[0];
            var read = await periods.TryReadAsync(request.TenantId, request.ProgramId, identity.PeriodId, ct).ConfigureAwait(false);
            if (!read.IsSuccess)
                return Result<ObservationPeriodCloseView>.Failure(read.Error!);
            var state = read.Value.View;
            var snapshot = state?.CloseSnapshots.SingleOrDefault(close => close.CloseSnapshotId == identity.CloseId);
            var fact = read.Value.FactById(identity.Source.EventId) as ITypeIIPeriodStateFact;
            if (state is null || snapshot is null || fact is null)
                return Conflict("The exact native close source is not visible yet.", true);
            if (identity.Source.Stream != read.Value.Stream || fact.Header.ProgramId != request.ProgramId ||
                fact.Header.LatestCloseSnapshotId != identity.CloseId || fact.Header.LatestCloseVersion != identity.Version ||
                fact.Revision != identity.PeriodRevision || snapshot.Version != identity.Version ||
                ContentDigest.OfJson(snapshot) != identity.SnapshotSha256 ||
                read.Value.FactById(identity.Source.EventId)!.Metadata!.AggregateVersion != identity.Source.AggregateVersion)
                return Conflict("The close locator differs from its exact authoritative historical snapshot.");
            result = Result<ObservationPeriodCloseView>.Success(new ObservationPeriodCloseView(request.TenantId, request.ProgramId,
                identity.CloseId, state.PeriodId, state.ExaminationId, snapshot, state.CloseSnapshots));
        }
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId, fence.Value, ct).ConfigureAwait(false);
        return confirmed.IsSuccess ? result : Result<ObservationPeriodCloseView>.Failure(confirmed.Error!);
    }
    static Result<ObservationPeriodCloseView> Conflict(string message, bool transient = false) =>
        Result<ObservationPeriodCloseView>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
