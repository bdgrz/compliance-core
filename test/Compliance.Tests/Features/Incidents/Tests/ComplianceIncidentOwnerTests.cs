using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Events;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
namespace Bdgrz.Compliance.Tests.Features.Incidents.Tests;

/// <summary>Owner-level proof that the incident stream admits only exact, in-scope, gap-free native facts.</summary>
public sealed class ComplianceIncidentOwnerTests
{
    [Fact]
    public async Task ShouldAppendInitialFactAtRevisionOneGivenOwnIncidentOperation()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var prepared = await PrepareAsync(fixture, fixture.Program, null);

        // Act
        var result = prepared.Owner.Append(prepared.Fact, prepared.Frozen, 0, 0);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.IsType<ComplianceIncidentRecorded>(Assert.Single(new AggregateScenario<ComplianceIncident>(prepared.Owner).PendingEvents));
        Assert.Equal(1, prepared.Owner.Revision);
        Assert.Equal(1, prepared.Owner.NativeSequence);
        Assert.Equal("recorded", prepared.Owner.Header!.Status);
        Assert.Equal(fixture.Program, prepared.Owner.ProgramId);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenMutationPreparedForAnotherProgram()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var prepared = await PrepareAsync(fixture, Uuid.CreateVersion4(), null);

        // Act
        var result = prepared.Owner.Append(prepared.Fact, prepared.Frozen, 0, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<ComplianceIncident>(prepared.Owner).PendingEvents);
        Assert.Null(prepared.Owner.Header);
    }

    [Fact]
    public async Task ShouldRefuseTransientlyGivenStaleSequenceWhenAppending()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var prepared = await PrepareAsync(fixture, fixture.Program, null);

        // Act
        var result = prepared.Owner.Append(prepared.Fact, prepared.Frozen, 5, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Empty(new AggregateScenario<ComplianceIncident>(prepared.Owner).PendingEvents);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenInitialFactWhoseOperationIsAnotherCommandKind()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var prepared = await PrepareAsync(fixture, fixture.Program, op => op with { CommandKind = nameof(ReviseComplianceIncident) });

        // Act
        var result = prepared.Owner.Append(prepared.Fact, prepared.Frozen, 0, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<ComplianceIncident>(prepared.Owner).PendingEvents);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenInitialFactCarryingAnExpectedRevision()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var prepared = await PrepareAsync(fixture, fixture.Program, op => op with { OriginalExpectedRevision = 1UL });

        // Act
        var result = prepared.Owner.Append(prepared.Fact, prepared.Frozen, 0, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<ComplianceIncident>(prepared.Owner).PendingEvents);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenSecondInitialReportOnRecordedIncident()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        await fixture.SeedAsync();
        var snapshot = await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident);
        var owner = snapshot.Owner;
        var again = await PrepareAsync(fixture, fixture.Program, null);

        // Act
        var result = owner.Append(again.Fact, again.Frozen, owner.NativeSequence, owner.CommittedStreamPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, owner.NativeSequence);
    }

    [Fact]
    public async Task ShouldRehydrateHeaderAndSequenceGivenRecordedAndRevisedHistory()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var body = fixture.Body();
        await fixture.SeedAsync(body);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        var revise = new ReviseComplianceIncident(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Incident, 1,
            body with { Title = "Corrected title" }, "Correction");
        Assert.True((await new Bdgrz.Compliance.Features.Incidents.Handlers.ReviseComplianceIncidentHandler(fixture.Operations)
            .HandleAsync(fixture.Context(revise), CancellationToken.None)).IsSuccess);

        // Act
        var snapshot = await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident);

        // Assert
        Assert.Equal(2, snapshot.Owner.Revision);
        Assert.Equal(2, snapshot.Owner.NativeSequence);
        Assert.Equal(fixture.Program, snapshot.Owner.ProgramId);
        Assert.Equal(2, snapshot.Owner.Header!.CurrentVersionNumber);
        Assert.NotNull(snapshot.Owner.CurrentContent);
    }

    static async Task<Prepared> PrepareAsync(IncidentNativeFixture fixture, Uuid ownerProgram, Func<OperationProvenance, OperationProvenance>? change)
    {
        var rules = new ComplianceIncidentRules(fixture.Tenant, fixture.Program, fixture.Incident);
        var member = Uuid.CreateVersion4();
        Assert.Null(rules.Record(fixture.Body(), member, "Incident member", fixture.Clock.Now));
        var owner = new ComplianceIncident(fixture.Tenant, ownerProgram, fixture.Incident);
        var frozen = await ComplianceIncidentContentStore.FreezeAsync(fixture.Content, null, null, null, rules,
            nameof(RecordComplianceIncident), CancellationToken.None);
        var op = OwnerProvenance.For(owner, fixture.Incident, nameof(RecordComplianceIncident), ActorReference.ForMember(member, "Incident member"),
            fixture.Incident.ToString(), null, new string('a', 64), fixture.Clock.Now) with
        {
            FirstOwner = owner.Stream,
        };
        if (change is not null)
            op = change(op);
        var response = await NativeContent.FreezeUnitAsync(fixture.Content, fixture.Tenant);
        var first = await FirstResponses.FreezeAsync(fixture.Content, fixture.Tenant.ToString(), response.Response);
        var fact = ComplianceIncidentContentStore.Fact(frozen, 1, op, first, response.Result);
        return new Prepared(owner, fact, frozen);
    }

    sealed record Prepared(ComplianceIncident Owner, DomainEvent Fact, FrozenComplianceIncidentMutation Frozen);
}
