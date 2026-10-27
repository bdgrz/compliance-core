using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

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
