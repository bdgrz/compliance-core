using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Starts <c>ControlOccurrenceFanOut</c> from a committed Type II selection. The process record, with the complete
///     canonical target list of the selection's manifest, exists before the source checkpoint advances; the sweep
///     resumes it if this reactor stops. A manifest that cannot be read yet is transient: no record is created from an
///     unverified selection.
/// </summary>
sealed partial class ControlOccurrenceFanOutReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(TypeIIPeriod.Area), WorkloadName),
        IReactorHandler<DueControlOccurrencesGenerated>
{
    public const string WorkloadName = "ControlOccurrenceFanOutV1";

    public async ValueTask HandleAsync(IReactorContext<DueControlOccurrencesGenerated> context, CancellationToken ct)
    {
        var selected = context.Trigger;
        ControlOccurrenceSelectionManifest manifest;
        try
        {
            manifest = await content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(
                context.Source.Stream.Realm, selected.CreationIntentManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The selection manifest cannot be verified yet: {exception.Message}", true));
        }

        _ = await process.StartAsync(ControlOccurrenceFanOut.ProcessName, context, selected.CreationIntentManifest,
            manifest.Selections.Select(static selection => selection.OccurrenceId.ToString()),
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
