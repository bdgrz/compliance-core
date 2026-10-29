using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonalWorkforceObservationTransportTests
{
    [Theory]
    [InlineData("reconcile", "direct")]
    [InlineData("reconcile", "mcp")]
    [InlineData("resolve", "direct")]
    [InlineData("resolve", "mcp")]
    public async Task ShouldRefusePersonalWorkforceDecisionGivenNonHttpInvocation(string action, string transport)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var position = await PositionAsync(fixture, action);
        var person = await ReadPersonAsync(fixture);

        // Act
        var result = await DecideAsync(fixture, action, transport);
        var afterPerson = await ReadPersonAsync(fixture);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(position, await PositionAsync(fixture, action));
        Assert.Equal(person.CommittedStreamPosition, afterPerson.CommittedStreamPosition);
        Assert.Equal(person.Revision, afterPerson.Revision);
    }

    [Theory]
    [InlineData("reconcile")]
    [InlineData("resolve")]
    public async Task ShouldRetainAttributedWorkforceDecisionGivenNativeHttp(string action)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var position = await PositionAsync(fixture, action);
        var beforePerson = await ReadPersonAsync(fixture);

        // Act
        var result = await DecideAsync(fixture, action, "http");
        var afterPerson = await ReadPersonAsync(fixture);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(position + 1, await PositionAsync(fixture, action));
        Assert.Equal(beforePerson.CommittedStreamPosition, afterPerson.CommittedStreamPosition);
        Assert.Equal(beforePerson.Revision, afterPerson.Revision);
        if (action == "reconcile")
        {
            var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
                new WorkforceSourceObservation(fixture.TenantId, fixture.SourceId));
            Assert.Equal(fixture.Source, source.Observation!.Source);
            Assert.Equal(fixture.Facts, source.Observation.Facts);
            Assert.Equal(fixture.PersonId, source.Observation.TargetId);
            Assert.Equal(1, source.Decision!.TargetRevision);
            Assert.Equal("accepted", source.Decision.Outcome);
            Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), source.Decision.Actor.Id);
        }
        else
        {
            WorkforceObservationResolved? closure = null;
            await foreach (var record in fixture.Provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "workforce-observation-resolutions"),
                EventCursor.Start, CancellationToken.None))
                if (record.Event is WorkforceObservationResolved resolved && resolved.ObservationId == fixture.ObservationId)
                    closure = resolved;
            Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), Assert.IsType<WorkforceObservationResolved>(closure).Actor.Id);
            Assert.Equal("resolved", closure.Resolution);
        }
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public async Task ShouldPreserveSourceAndTargetVersionGivenNativeHttpStaleReconciliation(long sourceRevision, long targetRevision)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var before = await PositionAsync(fixture, "reconcile");

        // Act
        var result = await DispatchAsync(fixture, new ReconcileWorkforceSourceObservation(fixture.TenantId,
            fixture.SourceId, sourceRevision, targetRevision, "accepted", "Checked facts."));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("The source or canonical workforce record changed. Current revision: 1. Reload it and retry.", result.Error?.Message);
        Assert.Equal(before, await PositionAsync(fixture, "reconcile"));
    }

    [Fact]
    public async Task ShouldPreserveCanonicalCorrectionRequirementGivenNativeHttpConflictingFacts()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        await ProgramManagementServices.SeedAsync(fixture.Provider, new PersonRules(fixture.TenantId, fixture.PersonId), person =>
        {
            Assert.Null(person.Revise(1, "Ada King", "ada@example.com", ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Client author"), DateTimeOffset.UtcNow));
            return Result.Success;
        });
        await fixture.CatchUpAsync();
        var before = await PositionAsync(fixture, "reconcile");
        var beforePerson = await ReadPersonAsync(fixture);

        // Act
        var result = await DispatchAsync(fixture, new ReconcileWorkforceSourceObservation(fixture.TenantId,
            fixture.SourceId, 1, 2, "accepted", "Checked facts."));
        var afterPerson = await ReadPersonAsync(fixture);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("Conflicting source facts require an explicit canonical revision or an attributed dismissal before reconciliation.", result.Error?.Message);
        Assert.Equal(before, await PositionAsync(fixture, "reconcile"));
        Assert.Equal(beforePerson.CommittedStreamPosition, afterPerson.CommittedStreamPosition);
        Assert.Equal(2, afterPerson.Revision);
    }

    [Theory]
    [InlineData("reconcile")]
    [InlineData("resolve")]
    public async Task ShouldPreserveIdenticalRetryGivenNativeHttpDecision(string action)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        Assert.True((await DecideAsync(fixture, action, "http")).IsSuccess);
        var before = await PositionAsync(fixture, action);

        // Act
        var result = await DecideAsync(fixture, action, "http");

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before, await PositionAsync(fixture, action));
    }

    [Theory]
    [InlineData("reconcile", "The source observation already has a different decision.")]
    [InlineData("resolve", "The workforce observation is already closed.")]
    public async Task ShouldPreserveFinalDecisionGivenNativeHttpChangedRetry(string action, string message)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        Assert.True((await DecideAsync(fixture, action, "http")).IsSuccess);
        var before = await PositionAsync(fixture, action);
        IRequest request = action == "reconcile"
            ? new ReconcileWorkforceSourceObservation(fixture.TenantId, fixture.SourceId, 2, 1, "dismissed", "Changed closure.")
            : new ResolveWorkforceObservation(fixture.TenantId, fixture.ObservationId, "dismissed", "Changed closure.");

        // Act
        var result = await DispatchAsync(fixture, request);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before, await PositionAsync(fixture, action));
    }

    [Theory]
    [InlineData("reconcile", "The source observation was not found.")]
    [InlineData("resolve", "The workforce observation was not found.")]
    public async Task ShouldPreserveObservationSourceGivenNativeHttpUnknownObservation(string action, string message)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var before = await PositionAsync(fixture, action);
        IRequest request = action == "reconcile"
            ? new ReconcileWorkforceSourceObservation(fixture.TenantId, Uuid.CreateVersion4(), 1, 1, "accepted", "Checked facts.")
            : new ResolveWorkforceObservation(fixture.TenantId, Uuid.CreateVersion4(), "resolved", "Checked facts.");

        // Act
        var result = await DispatchAsync(fixture, request);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before, await PositionAsync(fixture, action));
    }

    [Theory]
    [InlineData("reconcile")]
    [InlineData("resolve")]
    public async Task ShouldPreserveWorkforceGrantGivenNativeHttpWithoutOrdinaryPermission(string action)
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        fixture.Permissions.Allowed = false;
        var before = await PositionAsync(fixture, action);

        // Act
        var result = await DecideAsync(fixture, action, "http");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("The actor may not inspect or manage the workforce roster.", result.Error?.Message);
        Assert.Equal(before, await PositionAsync(fixture, action));
    }

    static async Task<Result> DispatchAsync(AttestWorkforceManagementWallTests.Fixture fixture, IRequest request)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId), Invocation("http")), CancellationToken.None);
    }

    internal static async Task<Result> HttpAsync(IServiceProvider provider, Uuid user, IRequest request,
        RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user), Invocation("http")), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static RequestInvocation Invocation(string transport) => transport switch
    {
        "direct" => new DirectInvocation(),
        "mcp" => new McpInvocation("synthetic.workforce.decision"),
        _ => new HttpInvocation("POST", "/synthetic/workforce/decision", "/synthetic/workforce/decision", "synthetic")
    };

    static async Task<Result> DecideAsync(AttestWorkforceManagementWallTests.Fixture fixture, string action, string transport)
    {
        IRequest request = action == "reconcile"
            ? new ReconcileWorkforceSourceObservation(fixture.TenantId, fixture.SourceId, 1, 1, "accepted", "Compared canonical source facts.")
            : new ResolveWorkforceObservation(fixture.TenantId, fixture.ObservationId, "resolved", "Reviewed joiner observation.");
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId), Invocation(transport)), CancellationToken.None);
    }

    static async Task<ulong> PositionAsync(AttestWorkforceManagementWallTests.Fixture fixture, string action) =>
        action == "reconcile"
            ? (await ProgramManagementServices.HydrateAsync(fixture.Provider, new WorkforceSourceObservation(fixture.TenantId, fixture.SourceId))).CommittedStreamPosition
            : (await ProgramManagementServices.HydrateAsync(fixture.Provider, new WorkforceObservationResolution(fixture.TenantId, fixture.ObservationId))).CommittedStreamPosition;

    static Task<Person> ReadPersonAsync(AttestWorkforceManagementWallTests.Fixture fixture) =>
        ProgramManagementServices.HydrateAsync(fixture.Provider, new Person(fixture.TenantId, fixture.PersonId));
}
