using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingCorrectiveActionOutcomeWorkflowTests
{
    [Theory]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public async Task ShouldKeepClosureRefusedUntilReportedGivenCommittedTerminalAction(string outcome)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, outcome);
        var before = await fixture.FindingStreamAsync(finding.FindingId);
        var refusal = before.PrepareClosure(before.Revision);
        var command = Report(fixture, finding, source, outcome);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command).ExpectSuccess();
        var after = await fixture.FindingStreamAsync(finding.FindingId);
        var history = await fixture.FindingHistoryAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refusal.Error?.Kind);
        Assert.Equal(1, before.UnresolvedActionCount);
        Assert.Equal(0, after.UnresolvedActionCount);
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.True(after.PrepareClosure(after.Revision).IsSuccess);
        var fact = Assert.Single(history.Facts.OfType<FindingCorrectiveActionResolved>());
        Assert.Equal(command.ActionId, fact.ActionId);
        Assert.Equal(outcome, fact.Outcome);
        Assert.Equal(command.Source, fact.Source);
    }

    [Fact]
    public async Task ShouldKeepRepeatedReportAsNoOpGivenCommittedSourceReportedAgain()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, "completed");
        var command = Report(fixture, finding, source, "completed");
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command).ExpectSuccess();
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command).ExpectSuccess();
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(0, after.UnresolvedActionCount);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFindingFactGivenOutcomeDisagreesWithCommittedSourceType()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, "cancelled");
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System)
            .When(Report(fixture, finding, source, "completed")).ExpectFailure(RequestErrorKind.Conflict);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(1, after.UnresolvedActionCount);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("finding")]
    [InlineData("action")]
    public async Task ShouldRefuseWithoutFindingFactGivenReportChangesSourceScope(string changed)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, "completed");
        var command = Report(fixture, finding, source, "completed");
        command = changed switch
        {
            "tenant" => command with { TenantId = Uuid.CreateVersion4() },
            "program" => command with { ProgramId = Uuid.CreateVersion4() },
            "finding" => command with { FindingId = Uuid.CreateVersion4() },
            _ => command with { ActionId = Uuid.CreateVersion4() },
        };
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command)
            .ExpectFailure(RequestErrorKind.Validation);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(1, after.UnresolvedActionCount);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFindingFactGivenSourceEventIdentityForged()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, "completed");
        var command = Report(fixture, finding, source, "completed");
        var forged = command.Source with { EventId = Uuid.CreateVersion4() };
        command = command with
        {
            Source = forged,
            CommandId = Uuid.CreateVersion5(forged.EventId, nameof(RecordCorrectiveActionOutcome)),
        };
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command)
            .ExpectFailure(RequestErrorKind.Conflict);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(1, after.UnresolvedActionCount);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFindingFactGivenPersonalOutcomeReport()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, "completed");

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(Report(fixture, finding, source, "completed"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(1, after.UnresolvedActionCount);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFindingFactGivenWrongPhysicalSourcePosition()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var (finding, source) = await TerminalAsync(fixture, "completed");
        var command = Report(fixture, finding, source, "completed") with { SourcePosition = source.ResourceOffset + 1 };
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(command)
            .ExpectFailure(RequestErrorKind.Conflict);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(1, after.UnresolvedActionCount);
    }

    internal static async Task<(FindingView Finding, DomainEventRecord Source)> TerminalAsync(OperationsFixture fixture,
        string outcome)
    {
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(4));
        var action = Assert.Single(finding.CorrectiveActions);
        finding = outcome == "completed"
            ? await fixture.AsAsync(fixture.OwnerUserId, fixture.Complete(finding, action.ActionId,
                "Removed access.", OperationsFixture.FullSupport))
            : await fixture.AsAsync(fixture.LeadUserId, new CancelCorrectiveAction(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, finding.FindingId, action.Revision, action.ActionId, "Duplicate work."));
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var stream = new CorrectiveAction(fixture.TenantId, fixture.ProgramId, finding.FindingId, action.ActionId).Stream;
        DomainEventRecord? source = null;
        await foreach (var record in events.ReadAsync(stream, 0))
            if (record.Event is CorrectiveActionCompletedV2 or CorrectiveActionCancelled)
                source = record;
        Assert.NotNull(source);
        return (finding, source);
    }

    internal static RecordCorrectiveActionOutcome Report(OperationsFixture fixture, FindingView finding,
        DomainEventRecord source, string outcome) => new(
        Uuid.CreateVersion5(source.Event.Metadata.EventId, nameof(RecordCorrectiveActionOutcome)), fixture.TenantId,
        fixture.ProgramId, finding.FindingId, Assert.Single(finding.CorrectiveActions).ActionId, outcome,
        NativeFactReference.Of(source.Stream, source.Event), source.ResourceOffset);
}
