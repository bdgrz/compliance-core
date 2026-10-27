using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The unordered control occurrence fan-out. The root is a Type II period's
///     <see cref="DueControlOccurrencesGenerated" /> selection; its targets are the canonical occurrence ids of the
///     selection's complete manifest, one <see cref="MaterializeControlOccurrence" /> receiver per target in canonical
///     key order. No receiver depends on another, and the canonical occurrence identity makes a crash, a replay or a
///     second generator that selects the same window converge on one occurrence.
/// </summary>
static class ControlOccurrenceFanOut
{
    public const string ProcessName = "ControlOccurrenceFanOut";
    public const string Materialize = "materialize";

    /// <summary>The only source generation of a selection; a selection is never retried under another generation.</summary>
    public const long Generation = 1;

    public static ProcessDefinition Definition() => new(ProcessName, [nameof(DueControlOccurrencesGenerated)], false,
        [
            new ProcessStageDefinition(Materialize, [nameof(MaterializeControlOccurrence)],
                "One receiver per selected window; canonical OccurrenceId dedupes generators."),
        ], [], Receiver);

    /// <summary>The command id the process driver derives for the receiver of one target of the selection.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string target, string episode) =>
        Kernel.EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{nameof(MaterializeControlOccurrence)}",
            target, episode);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        // A selection that is empty has no targets: the single untargeted step dispatches nothing.
        if (step.Target is null)
            return null;
        var record = step.Record;
        var realm = record.Key.Realm;
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        var occurrenceId = Uuid.Parse(step.Target, CultureInfo.InvariantCulture);
        var store = services.GetRequiredService<IImmutableContentStore>();
        var manifest = await store.ReadJsonAsync<ControlOccurrenceSelectionManifest>(realm, record.Intent, ct)
            .ConfigureAwait(false);
        if (manifest.Selections is null)
            throw new ImmutableContentIntegrityException("The selected occurrence manifest is incomplete.");
        ControlOccurrenceSelection? selection = null;
        foreach (var candidate in manifest.Selections.Where(candidate => candidate.OccurrenceId == occurrenceId))
        {
            if (selection is not null)
                throw new ImmutableContentIntegrityException("The process target has more than one frozen selection.");
            selection = candidate;
        }
        if (selection is null)
            throw new ImmutableContentIntegrityException("The process target is outside its exact frozen selection.");
        var intent = await store.ReadJsonAsync<ControlOccurrenceCreationIntent>(realm, selection.Intent, ct)
            .ConfigureAwait(false);

        var target = new ControlOccurrence(tenant, intent.ProgramId, intent.ControlId, intent.OccurrenceId).Stream;
        return new MaterializeControlOccurrence(step.CommandId, record.Source, target, record.Key.RootOperationId,
            Generation, null, selection.Intent, intent.ProgramId, intent.ControlId, intent.Window,
            intent.OccurrenceId);
    }
}
