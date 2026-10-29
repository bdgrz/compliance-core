using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Reads one occurrence with every attestation version and review decision.</summary>
public sealed class GetControlOccurrenceHandler(ControlReader controlSources, ControlOperationsReader operations,
    TimeProvider clock)
    : IRequestHandler<GetControlOccurrence, ControlOccurrenceView>
{
    public ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<GetControlOccurrence> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOccurrenceView>> HandleCoreAsync(
        IRequestContext<GetControlOccurrence> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOccurrenceView>.Failure(ControlOperationsSource.ControlNotFound());
        var snapshot = await operations.ReadOccurrenceAsync(request.TenantId, request.ProgramId, request.ControlId,
            request.OccurrenceId, ct).ConfigureAwait(false);
        return snapshot.ReadOccurrence(request.ControlId, request.OccurrenceId,
                OperatingAuthority.VersionWindows(control), ControlOperationsSource.Today(clock)) is
        { } view
            ? Result<ControlOccurrenceView>.Success(view)
            : Result<ControlOccurrenceView>.Failure(ControlOperationsSource.OccurrenceNotFound());
    }
}
