using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessExpectationIdentityTests
{
    [Fact]
    public async Task ShouldRefuseAmbiguousApprovalGivenSameSuccessorIdAdmittedOnIndependentLines()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (first, _, firstCommand, _) = await SeedConcurrentIdentityAsync(fixture);
        var command = fixture.Approve(firstCommand.ExpectationId, first.Revision);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ApproveAccessExpectationHandler>(scope.ServiceProvider);
        var context = new Cntryl.Portia.RequestContext<ApproveAccessExpectation>(command,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ApproverUserId),
                new HttpInvocation("PUT", "/access-expectations/approval", "/access-expectations/approval", "identity-test")));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess, "A scoped identity with two authoritative first owners must never choose the first one.");
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReplayEachOriginalProposalGivenSameScopedIdentityOnIndependentFirstOwners()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (first, second, firstCommand, secondCommand) = await SeedConcurrentIdentityAsync(fixture);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeAccessExpectationHandler>(scope.ServiceProvider);

        // Act
        var firstRetry = await handler.HandleAsync(new InventoryRequestContext<ProposeAccessExpectation>(firstCommand,
            ProgramManagementServices.Actor(fixture.ManagerUserId)), CancellationToken.None);
        var secondRetry = await handler.HandleAsync(new InventoryRequestContext<ProposeAccessExpectation>(secondCommand,
            ProgramManagementServices.Actor(fixture.ManagerUserId)), CancellationToken.None);

        // Assert
        Assert.True(firstRetry.IsSuccess, firstRetry.Error?.Message);
        Assert.True(secondRetry.IsSuccess, secondRetry.Error?.Message);
        Assert.Equal(first, firstRetry.Value);
        Assert.Equal(second, secondRetry.Value);
        Assert.NotEqual(first.SupersedesExpectationId, second.SupersedesExpectationId);
    }

    static async Task<(AccessExpectationView First, AccessExpectationView Second,
        ProposeAccessExpectation FirstCommand, ProposeAccessExpectation SecondCommand)> SeedConcurrentIdentityAsync(
        AccessReviewFixture fixture)
    {
        var parameters = new AccessExpectationParameters(PrincipalKind: "access_token");
        var rootA = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var rootB = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var shared = Uuid.CreateVersion4();
        var first = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: rootA.Revision,
            expectationId: shared, supersedes: rootA.ExpectationId, effectiveFrom: AccessReviewFixture.Observed);
        var second = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: rootB.Revision,
            expectationId: shared, supersedes: rootB.ExpectationId, effectiveFrom: AccessReviewFixture.Observed);
        var admittedA = await fixture.SendAsync(fixture.ManagerUserId, first);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // Both first owners can legally append after discovery ran before either candidate was visible. Hide only
        // the other admission from this command's scoped discovery; actual native preparation/append use real sources.
        var discovery = new AccessExpectationsReader(services.GetRequiredService<IAggregateReader>(),
            new BeforeOtherAdmission(services.GetRequiredService<IDomainEventReader>(), shared),
            services.GetRequiredService<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>());
        var handler = ActivatorUtilities.CreateInstance<ProposeAccessExpectationHandler>(services, discovery);
        var admittedB = await handler.HandleAsync(new InventoryRequestContext<ProposeAccessExpectation>(second,
            ProgramManagementServices.Actor(fixture.ManagerUserId)), CancellationToken.None);
        Assert.True(admittedB.IsSuccess, admittedB.Error?.Message);
        return (admittedA, admittedB.Value, first, second);
    }

    sealed class BeforeOtherAdmission(IDomainEventReader inner, Uuid shared) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => inner.ReadAsync(address, fromOffset, ct);
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct).ConfigureAwait(false))
                if (record.Event is not AccessExpectationProposed proposed || proposed.Operation.LogicalId != shared)
                    yield return record;
        }
    }
}
