using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Native operation provenance for application and system instance facts seeded straight into streams.</summary>
static class ApplicationFacts
{
    static readonly DateTimeOffset Accepted = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh root operation owned by the application's own stream.</summary>
    public static OperationProvenance Application(Uuid tenantId, Uuid applicationId, Uuid? logicalId = null) =>
        Operation(new EventStreamAddress(tenantId.ToString(), DeclaredApplication.Area, applicationId.ToString()),
            logicalId);

    /// <summary>A fresh root operation owned by the system instance's own stream.</summary>
    public static OperationProvenance SystemInstance(Uuid tenantId, Uuid systemInstanceId, Uuid? logicalId = null) =>
        Operation(new EventStreamAddress(tenantId.ToString(), DeclaredSystemInstance.Area,
            systemInstanceId.ToString()), logicalId);

    /// <summary>A fresh root operation owned by the given aggregate's own stream.</summary>
    public static OperationProvenance For(Aggregate owner) => Operation(owner.Stream, null);

    static OperationProvenance Operation(EventStreamAddress owner, Uuid? logicalId)
    {
        var id = logicalId ?? Uuid.CreateVersion4();
        return new OperationProvenance(id, id, null, "Seed", ActorReference.ForMember(Uuid.CreateVersion4(), "Seed"),
            owner, "initial", null, "seed", Accepted, owner, owner);
    }
}
