using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>
///     One stage of an ordered process and the receivers it dispatches. <paramref name="PerTarget" /> overrides the
///     stage-name convention of <see cref="IsPerTarget" /> for a stage whose name does not say whether it dispatches
///     once per canonical target.
/// </summary>
public sealed record ProcessStageDefinition(string Stage, IReadOnlyList<string> Receivers, string? Rule = null,
    bool? PerTarget = null)
{
    /// <summary>
    ///     How the stage walks the record's canonical targets. The default follows the stage name, which is how every
    ///     process before the interleaved stage behaves.
    /// </summary>
    public StageTargeting Targeting { get; init; } = StageTargeting.ByStageName;

    /// <summary>Stages that acquire keys dispatch once per canonical target; others dispatch once.</summary>
    public bool IsPerTarget => PerTarget ?? (Targeting switch
    {
        StageTargeting.Once => false,
        StageTargeting.EachReceiver or StageTargeting.Interleaved => true,
        _ => Stage is "acquire" or "reserve" or "claim" or "protect" or "authorize" or "release" or "settle" or "materialize",
    });

    /// <summary>
    ///     True when the canonical targets are the outer loop and the receivers the inner one, so one global key order
    ///     is walked and every target offers itself to every receiver in turn.
    /// </summary>
    public bool IsInterleaved => Targeting == StageTargeting.Interleaved;

    /// <summary>Stages that still run after an abort has fenced the receiving operation.</summary>
    public bool RunsAfterAbort => Stage is "settle" or "release";
}
