using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>
///     One treatment of a (risk, control) assertion line as the line's facts left it: the proposal, the review that
///     decided it and the retirement that withdrew it. The line identity is the logical id of the proposal.
/// </summary>
public sealed class RiskTreatmentLine(RiskControlTreatmentProposed proposed)
{
    /// <summary>The proposal fact.</summary>
    public RiskControlTreatmentProposed Proposed { get; } = proposed;

    /// <summary>The treatment identity: the logical id of the proposing operation.</summary>
    public Uuid TreatmentId => Proposed.Operation.LogicalId;

    /// <summary>The proposer member recorded with the proposal.</summary>
    public Uuid ProposerMemberId => Proposed.ProposerMemberId;

    /// <summary>The current status.</summary>
    public string Status { get; internal set; } = RiskStates.TreatmentProposed;

    /// <summary>The review fact, once decided.</summary>
    public RiskControlTreatmentReviewed? Reviewed { get; internal set; }

    /// <summary>The retirement fact, once withdrawn.</summary>
    public RiskControlTreatmentRetired? Retired { get; internal set; }
}
