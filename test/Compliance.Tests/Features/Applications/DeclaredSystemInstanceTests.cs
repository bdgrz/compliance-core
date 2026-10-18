using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclaredSystemInstanceTests
{
    [Fact]
    public void ShouldKeepApplicationRevisionIndependentGivenTwoInstanceDeclarations()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null,
            actorId, "Manager", now).IsSuccess);
        var first = new DeclaredSystemInstance(tenantId, Uuid.CreateVersion4());
        var second = new DeclaredSystemInstance(tenantId, Uuid.CreateVersion4());

        // Act
        var firstResult = first.Declare(applicationId, "Production", "production",
            null, null, actorId, "Manager", now);
        var secondResult = second.Declare(applicationId, "Staging", "staging",
            null, null, actorId, "Manager", now);
        var revised = application.Revise(1, "Payroll", "Monthly payroll", null,
            actorId, "Manager", now);

        // Assert
        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Null(revised);
        Assert.Equal(2, application.Revision);
        Assert.Equal(1, first.Revision);
        Assert.Equal(1, second.Revision);
        Assert.NotEqual(first.Stream, second.Stream);
        Assert.NotEqual(application.Stream, first.Stream);
        Assert.Collection(new AggregateScenario<DeclaredApplication>(application).PendingEvents,
            ev => Assert.IsType<ApplicationDeclared>(ev),
            ev => Assert.IsType<ApplicationRevised>(ev));
    }

    [Fact]
    public void ShouldOwnImmutableParentAndRevisionGivenDeclarationAndRetry()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var instance = new DeclaredSystemInstance(tenantId, instanceId);

        // Act
        var declared = instance.Declare(applicationId, " Production ", "production",
            null, " payroll-prod ", actorId, "Manager", now);
        var retry = instance.Declare(applicationId, "Production", "production",
            null, "payroll-prod", actorId, "Manager", now.AddMinutes(1));
        var wrongParent = instance.Declare(Uuid.CreateVersion4(), "Production", "production",
            null, "payroll-prod", actorId, "Manager", now.AddMinutes(2));
        var collision = instance.Declare(applicationId, "Staging", "production",
            null, "payroll-prod", actorId, "Manager", now.AddMinutes(3));

        // Assert
        Assert.True(declared.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(wrongParent.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(collision.Error).Kind);
        Assert.Equal(applicationId, instance.ApplicationId);
        Assert.Equal(1, instance.Revision);
        var ev = Assert.IsType<SystemInstanceRegistered>(
            Assert.Single(new AggregateScenario<DeclaredSystemInstance>(instance).PendingEvents));
        Assert.Equal(tenantId, ev.TenantId);
        Assert.Equal(instanceId, ev.SystemInstanceId);
        Assert.Equal(1, ev.Revision);
        Assert.Equal("Production", ev.Name);
        Assert.Equal("payroll-prod", ev.SourceIdentifier);
        Assert.Equal(ActorReference.ForMember(actorId, "Manager"), ev.StoredActor);
        Assert.Equal(ActorReference.ForMember(actorId, "Manager"), ev.Actor);
    }

    [Fact]
    public void ShouldReuseCommittedIdentityGivenReplayedInstanceDeclaration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        new AggregateScenario<DeclaredSystemInstance>(instance).Given(DomainEventSeed.Attach(
            new SystemInstanceRegistered(tenantId, applicationId, instanceId, 1,
                "Production", "production", null, "payroll-prod", actorId,
                "Manager", now), instanceId, 1));

        // Act
        var retry = instance.Declare(applicationId, "Production", "production",
            null, "payroll-prod", actorId, "Changed display", now.AddDays(1));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(instanceId, retry.Value.SystemInstanceId);
        Assert.Equal(1, instance.Revision);
        Assert.Empty(new AggregateScenario<DeclaredSystemInstance>(instance).PendingEvents);
    }

    [Fact]
    public void ShouldReviseInstanceWithoutChangingItsParentGivenCurrentRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        Assert.True(instance.Declare(applicationId, "Production", "production", "Boundary v1",
            "payroll-prod", actorId, "Manager", now).IsSuccess);

        // Act
        var revised = instance.Revise(1, "Production east", "payroll-prod-east", "Boundary v2",
            "The production region moved to the east network zone.", actorId, "Manager", now.AddMinutes(1));
        var events = new AggregateScenario<DeclaredSystemInstance>(instance).PendingEvents.ToArray();
        var replay = new AggregateScenario<DeclaredSystemInstance>(
            new DeclaredSystemInstance(tenantId, instanceId)).Given(events).Aggregate;

        // Assert
        Assert.Null(revised);
        Assert.Equal(applicationId, instance.ApplicationId);
        Assert.Equal(2, instance.Revision);
        Assert.Equal("Production east", instance.Name);
        Assert.Equal("payroll-prod-east", instance.SourceIdentifier);
        Assert.Equal("Boundary v2", instance.AccessBoundaryReference);
        var ev = Assert.IsType<SystemInstanceRevised>(events[^1]);
        Assert.Equal(applicationId, ev.ApplicationId);
        Assert.Equal("The production region moved to the east network zone.", ev.Rationale);
        Assert.Equal(ActorReference.ForMember(actorId, "Manager"), ev.Actor);
        Assert.Equal(instance.Name, replay.Name);
        Assert.Equal(instance.SourceIdentifier, replay.SourceIdentifier);
        Assert.Equal(instance.AccessBoundaryReference, replay.AccessBoundaryReference);
    }

    [Fact]
    public void ShouldRejectStaleInstanceRevisionGivenChangedContent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var instance = new DeclaredSystemInstance(tenantId, Uuid.CreateVersion4());
        Assert.True(instance.Declare(applicationId, "Production", "production", "Boundary v1",
            "payroll-prod", actorId, "Manager", now).IsSuccess);

        // Act
        var failure = instance.Revise(0, "Production east", "payroll-prod-east", "Boundary v2",
            "The production region moved.", actorId, "Manager", now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, instance.Revision);
        Assert.Equal("Production", instance.Name);
        Assert.Single(new AggregateScenario<DeclaredSystemInstance>(instance).PendingEvents);
    }
}
