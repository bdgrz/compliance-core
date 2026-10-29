using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Models;

/// <summary>
///     The governance of one risk as its own owners left it: the owner and triggers of its governance stream, the
///     treatment assertion lines and the treatment actions composed from their streams. This is a captured read-side
///     view and is never retained as command state.
/// </summary>
public sealed class RiskGovernanceRecord(long revision, RiskOwnerView? owner,
    IReadOnlyList<RiskControlTreatmentView> treatments, IReadOnlyList<RiskReassessmentTriggerView> triggers,
    IReadOnlyList<RiskTreatmentActionView> actions, IReadOnlyDictionary<(Uuid ActionId, Uuid SubmissionId), Uuid> submitters)
{
    /// <summary>An empty record for a risk with no governance facts yet.</summary>
    public static RiskGovernanceRecord Empty { get; } = new(0, null, [], [], [], new Dictionary<(Uuid ActionId, Uuid SubmissionId), Uuid>());

    /// <summary>The revision of the risk's governance stream.</summary>
    public long Revision { get; } = revision;

    /// <summary>The current owner, or null.</summary>
    public RiskOwnerView? Owner { get; } = owner;

    /// <summary>Every control treatment of every line of the risk, in the order proposed.</summary>
    public IReadOnlyList<RiskControlTreatmentView> Treatments { get; } = treatments;

    /// <summary>Every trigger in the order raised.</summary>
    public IReadOnlyList<RiskReassessmentTriggerView> Triggers { get; } = triggers;

    /// <summary>Every action of the risk in the order added.</summary>
    public IReadOnlyList<RiskTreatmentActionView> Actions { get; } = actions;

    /// <summary>The submitting member of every completion submission.</summary>
    public IReadOnlyDictionary<(Uuid ActionId, Uuid SubmissionId), Uuid> Submitters { get; } = submitters;
}
