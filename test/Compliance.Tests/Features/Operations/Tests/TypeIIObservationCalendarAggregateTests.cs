using System.Text;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class TypeIIObservationCalendarAggregateTests
{
    [Fact]
    public void ShouldKeepOneStreamPerProgramGivenAnySelectedLogicalId()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();

        // Act
        var plain = new TypeIIObservationCalendar(tenantId, programId);
        var selected = new TypeIIObservationCalendar(tenantId, programId, Uuid.CreateVersion4());
        var other = new TypeIIObservationCalendar(tenantId, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(new EventStreamAddress(tenantId.ToString(), TypeIIObservationCalendar.Area, programId.ToString()), plain.Stream);
        Assert.Equal(plain.Stream, selected.Stream);
        Assert.Equal(programId, selected.Id);
        Assert.NotEqual(plain.Stream, other.Stream);
        Assert.Equal(0L, plain.Revision);
        Assert.Equal(0L, plain.ReservationGeneration);
    }

    [Fact]
    public async Task ShouldAppendOneAuthorizationGivenExactPreparedDecisionAndSelectedLogicalId()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (commandId, prepared) = await PrepareAsync(fixture);
        var owner = new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, commandId);

        // Act
        var result = owner.Append(TypeIICalendarContent.Fact(prepared), prepared, owner.NativeSequence, owner.CommittedStreamPosition);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(new AggregateScenario<TypeIIObservationCalendar>(owner).PendingEvents);
        Assert.Equal(1L, owner.Revision);
        Assert.Equal(1L, owner.ReservationGeneration);
        Assert.Equal(1L, owner.NativeSequence);
        Assert.Equal(prepared.Authority, owner.CurrentAuthority);
        Assert.NotNull(owner.Acceptance.SelectedFact);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenAnotherSelectedLogicalId()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (_, prepared) = await PrepareAsync(fixture);
        var owner = new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4());

        // Act
        var result = owner.Append(TypeIICalendarContent.Fact(prepared), prepared, owner.NativeSequence, owner.CommittedStreamPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Empty(new AggregateScenario<TypeIIObservationCalendar>(owner).PendingEvents);
        Assert.Equal(0L, owner.Revision);
    }

    [Fact]
    public async Task ShouldRefuseTransientlyGivenStaleNativeSequenceAfterCompetingAppend()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (commandId, prepared) = await PrepareAsync(fixture);
        var owner = new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, commandId);
        var staleSequence = owner.NativeSequence;
        var stalePosition = owner.CommittedStreamPosition;
        Assert.True(owner.Append(TypeIICalendarContent.Fact(prepared), prepared, staleSequence, stalePosition).IsSuccess);

        // Act
        var result = owner.Append(TypeIICalendarContent.Fact(prepared), prepared, staleSequence, stalePosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Single(new AggregateScenario<TypeIIObservationCalendar>(owner).PendingEvents);
        Assert.Equal(1L, owner.Revision);
    }

    [Fact]
    public async Task ShouldAppendNoSecondFactGivenRetryOfAcceptedAuthorizationAtCurrentSequence()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (commandId, prepared) = await PrepareAsync(fixture);
        var owner = new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, commandId);
        Assert.True(owner.Append(TypeIICalendarContent.Fact(prepared), prepared, owner.NativeSequence, owner.CommittedStreamPosition).IsSuccess);

        // Act
        var result = owner.Append(TypeIICalendarContent.Fact(prepared), prepared, owner.NativeSequence, owner.CommittedStreamPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(new AggregateScenario<TypeIIObservationCalendar>(owner).PendingEvents);
        Assert.Equal(1L, owner.Revision);
        Assert.Equal(1L, owner.NativeSequence);
    }

    [Fact]
    public async Task ShouldRefuseGivenFactThatDiffersFromPreparedDecision()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (commandId, prepared) = await PrepareAsync(fixture);
        var owner = new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, commandId);
        var tampered = prepared with { Authority = prepared.Authority with { Sha256 = new string('c', 64) } };

        // Act
        var result = owner.Append(TypeIICalendarContent.Fact(prepared), tampered, owner.NativeSequence, owner.CommittedStreamPosition);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<TypeIIObservationCalendar>(owner).PendingEvents);
        Assert.Equal(0L, owner.Revision);
    }

    [Fact]
    public async Task ShouldRebuildHeaderAuthorityAndAcceptanceGivenReplayOfAuthorization()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (commandId, prepared) = await PrepareAsync(fixture);
        var source = new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, commandId);
        Assert.True(source.Append(TypeIICalendarContent.Fact(prepared), prepared, source.NativeSequence, source.CommittedStreamPosition).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId, commandId), history);

        // Assert
        Assert.Equal(source.Revision, replayed.Revision);
        Assert.Equal(source.ReservationGeneration, replayed.ReservationGeneration);
        Assert.Equal(source.NativeSequence, replayed.NativeSequence);
        Assert.Equal(source.Header, replayed.Header);
        Assert.Equal(prepared.Authority, replayed.CurrentAuthority);
        Assert.NotNull(replayed.Acceptance.SelectedFact);
    }

    static async Task<(Uuid CommandId, TypeIICalendarPrepared Prepared)> PrepareAsync(TypeIIRangeFixture fixture)
    {
        var pending = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4());
        var period = await fixture.PeriodAsync(pending);
        var accepted = period.AcceptedFor(pending)!;
        var intentReference = TypeIIPeriod.IntentOf(accepted)!;
        var intent = await fixture.Host.Content.ReadJsonAsync<TypeIIRangeIntent>(fixture.TenantId.ToString(), intentReference);
        var commandId = TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata.EventId, nameof(AcquireTypeIIPeriodRange), intent.Header.Episode);
        var operation = TypeIIRangeEffects.Child(commandId, nameof(AcquireTypeIIPeriodRange), accepted.Metadata.EventId,
            intent, fixture.CalendarStream, TypeIIRangeFixture.StartedAt);
        var selected = await fixture.Host.Calendars.ReadAsync(fixture.TenantId, fixture.ProgramId, [pending]);
        Assert.True(selected.Rules.Authorize(operation, intentReference, pending, intent.OperationKind, intent.NewRange, null).IsSuccess);
        var prepared = await TypeIICalendarContent.FreezeAsync(fixture.Host.Content, selected, selected.Rules);
        return (commandId, prepared);
    }
}
