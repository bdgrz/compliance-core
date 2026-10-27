using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
namespace Bdgrz.Compliance.Features.Workforce.Aggregates;

/// <summary>
///     Owns the attributed closure of one workforce observation, keyed by the observation ID. A
///     closure is final: when the underlying facts change, a new observation is issued instead.
/// </summary>
public sealed class WorkforceObservationResolution : Aggregate
{
    static readonly string[] Outcomes = ["resolved", "dismissed"];

    readonly Uuid _tenantId;
    string? _resolution;
    string? _note;

    public bool IsResolved => _resolution is not null;

    public WorkforceObservationResolution(Uuid tenantId, Uuid observationId)
        : base(observationId, new EventStreamAddress(tenantId.ToString(),
            "workforce-observation-resolutions", observationId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkforceObservationResolved>(ev =>
        {
            _resolution = ev.Resolution;
            _note = ev.Note;
        });
    }

    public CommandFailure? Resolve(string resolution, string note, ActorReference actor,
        DateTimeOffset resolvedAt)
    {
        if (!Outcomes.Contains(resolution))
            return CommandFailure.InvalidContent("The resolution must be resolved or dismissed.");
        var clean = note?.Trim() ?? string.Empty;
        if (clean.Length is 0 or > 1000)
            return CommandFailure.InvalidContent(
                "A resolution requires a note of 1 to 1000 characters.");
        if (_resolution is not null)
            return _resolution == resolution && _note == clean
                ? null
                : CommandFailure.StateConflict("The workforce observation is already closed.");
        RaiseEvent(new WorkforceObservationResolved(_tenantId, Id, resolution, clean, actor,
            resolvedAt));
        return null;
    }
}
