using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>Lifecycle of a process record. Only Pending and Aborting records are resumed.</summary>
public enum ProcessState
{
    /// <summary>Stages remain to dispatch.</summary>
    Pending,

    /// <summary>An abort was requested; abort receivers fence first, then settle/release stages run.</summary>
    Aborting,

    /// <summary>Every stage completed, or a receiver proved supersession.</summary>
    Completed,

    /// <summary>Abort receivers and settlement stages completed.</summary>
    Aborted,

    /// <summary>A receiver returned conflicting intent or an invalid proof; repair is manual.</summary>
    Quarantined,
}
