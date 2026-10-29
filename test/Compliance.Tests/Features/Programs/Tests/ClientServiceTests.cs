using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ClientServiceTests
{
    [Fact]
    public async Task ShouldPreserveActorSnapshotGivenChangedDisplayAndLegacyReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var service = new ProductClientServiceRules(tenantId, serviceId);
        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Original display", now).IsSuccess);
        Assert.Null(service.Revise(1, "Payroll", "Monthly payroll", "Operations",
            actorId, "Changed display", now.AddMinutes(1)));
        Assert.Null(service.Retire(2, "Service ended", actorId, "Final display",
            now.AddMinutes(2)));

        // Act
        await using var native = new ProductClientServiceFixture();
        var events = await ProductClientServiceSeeds.FactsAsync(native.Content, service);
        var created = Assert.IsType<ClientServiceCreated>(events[0]);
        var revised = Assert.IsType<ClientServiceRevised>(events[1]);
        var retired = Assert.IsType<ClientServiceRetired>(events[2]);
        var legacyCreated = JsonSerializer.Deserialize(JsonSerializer.Serialize(created, ComplianceCoreJsonContext.Default.ClientServiceCreated), ComplianceCoreJsonContext.Default.ClientServiceCreated);
        var legacyRevised = JsonSerializer.Deserialize(JsonSerializer.Serialize(revised, ComplianceCoreJsonContext.Default.ClientServiceRevised), ComplianceCoreJsonContext.Default.ClientServiceRevised);
        var legacyRetired = JsonSerializer.Deserialize(JsonSerializer.Serialize(retired, ComplianceCoreJsonContext.Default.ClientServiceRetired), ComplianceCoreJsonContext.Default.ClientServiceRetired);
        var legacyCurrent = JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{tenantId}}","service_id":"{{serviceId}}","revision":3,
             "name":"Payroll","purpose":"Monthly payroll","owner_reference":"Operations",
             "status":"retired","last_changed_by_member_id":"{{actorId}}",
             "last_changed_by_display":"Final display",
             "last_changed_at":"{{now.AddMinutes(2):O}}","program_id":"{{programId}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceView);
        var legacyHistory = JsonSerializer.Deserialize($$"""
            {"service_id":"{{serviceId}}","revision":1,"name":"Payroll",
             "purpose":"Process payroll","owner_reference":"Operations","status":"active",
             "retirement_rationale":null,"actor_member_id":"{{actorId}}",
             "actor_display":"Original display","changed_at":"{{now:O}}",
             "program_id":"{{programId}}"}
            """, ComplianceCoreJsonContext.Default.ClientServiceRevisionView);

        // Assert
        Assert.Equal(ActorReference.ForMember(actorId, "Original display"), created.Operation.Actor);
        Assert.Equal(ActorReference.ForMember(actorId, "Changed display"), revised.Operation.Actor);
        Assert.Equal(ActorReference.ForMember(actorId, "Final display"), retired.Operation.Actor);
        Assert.Equal(created.Operation.Actor, legacyCreated?.Actor);
        Assert.Equal(revised.Operation.Actor, legacyRevised?.Actor);
        Assert.Equal(retired.Operation.Actor, legacyRetired?.Actor);
        Assert.Equal(created.Operation, legacyCreated?.Operation);
        Assert.Equal(revised.Operation, legacyRevised?.Operation);
        Assert.Equal(retired.Operation, legacyRetired?.Operation);
        Assert.Equal(retired.Actor, legacyCurrent?.LastChangedBy);
        Assert.Equal(created.Actor, legacyHistory?.Actor);
        Assert.Equal(3, service.Revision);
        Assert.False(service.IsActive);
    }

    [Fact]
    public async Task ShouldReadCurrentActivityFromTenantEventStreamGivenCreateAndRetire()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        await using var fixture = new ProductClientServiceFixture();
        var activity = new EventSourcedClientServiceActivity(fixture.Reader);

        // Act
        var service = new ProductClientServiceRules(tenantId, serviceId);

        // Assert
        Assert.False(await activity.IsActiveAsync(tenantId, programId, serviceId));

        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        await fixture.PublishAsync(service);
        Assert.True(await activity.IsActiveAsync(tenantId, programId, serviceId));
        Assert.False(await activity.IsActiveAsync(Uuid.CreateVersion4(), programId, serviceId));
        Assert.False(await activity.IsActiveAsync(tenantId, Uuid.CreateVersion4(), serviceId));

        service = await fixture.Services.RulesAsync(tenantId, serviceId);
        Assert.Null(service.Retire(1, "Service ended", actorId, "Owner", now.AddDays(1)));
        await fixture.PublishAsync(service);
        Assert.False(await activity.IsActiveAsync(tenantId, programId, serviceId));
    }

    [Fact]
    public void ShouldRejectChangedCreateGivenExistingServiceAndPreserveReplay()
    {
        // Arrange
        var service = new ProductClientServiceRules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        Assert.Null(service.Revise(1, "Payroll", "Monthly payroll", "Operations",
            actorId, "Owner", now.AddMinutes(1)));

        // Act
        var replay = service.Create(programId, " Payroll ", "Process payroll", "Operations",
            actorId, "Owner", now.AddMinutes(2));
        var changed = service.Create(programId, "Payroll", "Different purpose", "Operations",
            actorId, "Owner", now.AddMinutes(2));
        var otherProgram = service.Create(Uuid.CreateVersion4(), "Payroll", "Process payroll",
            "Operations", actorId, "Owner", now.AddMinutes(2));

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(otherProgram.Error).Kind);
        Assert.Equal(2, service.AppliedChanges.Count);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionAndInvalidRetirementGivenServiceHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

        // Act
        var service = new ProductClientServiceRules(tenantId, serviceId);


        // Assert
        Assert.True(service.Create(programId, "Payroll", "Process payroll", "Operations",
            actorId, "Owner", now).IsSuccess);
        var stale = Assert.IsType<CommandFailure>(service.Revise(0,
            "Payroll", "Changed", "Operations", actorId, "Owner", now));
        Assert.Equal(CommandFailureCode.VersionConflict, stale.Code);
        Assert.Equal(1, stale.Version!.CurrentRevision);
        Assert.Null(service.Revise(1, "Payroll", "Monthly payroll", "Operations",
            actorId, "Owner", now.AddDays(1)));
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(service.Retire(2,
            " ", actorId, "Owner", now)).Code);
        Assert.Null(service.Retire(2, "Service ended", actorId, "Owner",
            now.AddDays(2)));
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(service.Revise(3,
            "Payroll", "Changed", "Operations", actorId, "Owner", now)).Code);
        await using var native = new ProductClientServiceFixture();
        var facts = await ProductClientServiceSeeds.FactsAsync(native.Content, service);
        var retiredFact = Assert.IsType<ClientServiceRetired>(facts[^1]);
        var retirement = await ProductClientServiceReader.ReadRetirementAsync(native.Content, retiredFact);
        Assert.Collection(facts,
            ev => Assert.IsType<ClientServiceCreated>(ev),
            ev => Assert.Equal(2, Assert.IsType<ClientServiceRevised>(ev).Revision),
            ev =>
            {
                Assert.Equal(retiredFact.Retirement, Assert.IsType<ClientServiceRetired>(ev).Retirement);
                Assert.Equal("Service ended", retirement.Rationale);
            });
    }
}
