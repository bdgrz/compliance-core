using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     The facts of one personal decision on its engagement owner, in the order they were appended. The ordinal is the
///     admission order on the owner; it decides which materialized decision stands for its kind.
/// </summary>
public sealed class EngagementPersonalDecisionRecord(EngagementPersonalDecisionAdmitted admitted, long ordinal)
{
    /// <summary>The root admission, which freezes the decision intent and pins the engagement content.</summary>
    public EngagementPersonalDecisionAdmitted Admitted { get; } = admitted;

    /// <summary>The admission order of this decision on the engagement owner.</summary>
    public long Ordinal { get; } = ordinal;

    /// <summary>The irreversible authorization, once appended.</summary>
    public EngagementPersonalDecisionAuthorized? Authorized { get; internal set; }

    /// <summary>The pre-authorization cancellation, once appended.</summary>
    public EngagementPersonalDecisionCanceled? Canceled { get; internal set; }

    /// <summary>The materialization, once appended; it releases the engagement content pin.</summary>
    public EngagementPersonalDecisionMaterialized? Materialized { get; internal set; }

    /// <summary>True while the decision still pins the engagement content.</summary>
    public bool IsPending => Canceled is null && Materialized is null;
}
