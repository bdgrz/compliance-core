using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
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
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Whether a restoration receiver may apply its source change now.</summary>
public enum GateDecision
{
    /// <summary>No earlier incompatible withdrawal is unfinished and no later one superseded the restoration.</summary>
    Proceed,

    /// <summary>An earlier withdrawal of the same episode and reason is unfinished.</summary>
    Wait,

    /// <summary>A later withdrawal of the same episode and reason makes the restoration moot.</summary>
    Superseded,

    /// <summary>The guard holds no pending operation with that identity.</summary>
    Unknown,
}
