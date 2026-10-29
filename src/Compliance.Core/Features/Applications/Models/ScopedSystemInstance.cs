using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
/// The facts a scope decision needs about one system instance, whether it was registered in its
/// own stream or declared in a legacy application stream.
/// </summary>
public sealed record ScopedSystemInstance(Uuid Id, Uuid ApplicationId, long Revision,
    bool IsRetired, Uuid RegisteredByMemberId)
{
    public Uuid? AccessOwnerMemberId { get; init; }

    public static ScopedSystemInstance From(DeclaredSystemInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new ScopedSystemInstance(instance.Id, instance.ApplicationId, instance.Revision,
            instance.IsRetired, instance.RegisteredByMemberId);
    }

    /// <summary>A legacy application-stream declaration has one immutable revision.</summary>
    public static ScopedSystemInstance FromLegacy(SystemInstanceDeclared declared)
    {
        ArgumentNullException.ThrowIfNull(declared);
        return new ScopedSystemInstance(declared.SystemInstanceId, declared.ApplicationId, 1,
            false, declared.ActorMemberId);
    }
}
