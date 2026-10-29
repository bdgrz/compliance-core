using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessExpectationPreparationReplayTests
{
    [Fact]
    public async Task ShouldReplayEqualProposedWinnerGivenLosingContentFreezeFailsAfterConcurrentAdmission()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var request = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind,
            new AccessExpectationParameters(PrincipalKind: "access_token"));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        AccessExpectationView? winner = null;
        var store = new LosingStore(services.GetRequiredService<IImmutableContentStore>(), async () =>
            winner = await fixture.SendAsync(fixture.ManagerUserId, request));
        var operations = new NativeOperationExecutor(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = ActivatorUtilities.CreateInstance<ProposeAccessExpectationHandler>(services, operations, store);
        var context = new InventoryRequestContext<ProposeAccessExpectation>(request,
            ProgramManagementServices.Actor(fixture.ManagerUserId));
        Result<AccessExpectationView> result = default;

        // Act
        var error = await Record.ExceptionAsync(async () => result = await handler.HandleAsync(context, CancellationToken.None));
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessExpectation(fixture.TenantId, request.ExpectationId));

        // Assert
        Assert.Null(error);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(winner, result.Value);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, store.CompetingAdmissions);
    }

    [Fact]
    public async Task ShouldReplayOriginalProposalGivenLaterSuccessorBodyUnavailable()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var parameters = new AccessExpectationParameters(PrincipalKind: "access_token");
        var request = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var first = await fixture.SendAsync(fixture.ManagerUserId, request);
        var approved = await fixture.SendAsync(fixture.ApproverUserId, fixture.Approve(first.ExpectationId, first.Revision));
        var successor = await fixture.SendAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: approved.Revision,
            supersedes: first.ExpectationId, rationale: "Later independent successor.",
            effectiveFrom: AccessReviewFixture.Observed));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var events = services.GetRequiredService<IDomainEventReader>();
        ImmutableContentRef? unavailable = null;
        var stream = new AccessExpectation(fixture.TenantId, first.ExpectationId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, CancellationToken.None))
            if (record.Event is AccessExpectationProposed proposal && proposal.Operation.LogicalId == successor.ExpectationId)
                unavailable = proposal.ExpectationContent;
        Assert.NotNull(unavailable);
        var store = new MissingSuccessorStore(services.GetRequiredService<IImmutableContentStore>(), unavailable);
        var expectations = new AccessExpectationsReader(reader, events, store);
        var operations = new NativeOperationExecutor(reader, services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = ActivatorUtilities.CreateInstance<ProposeAccessExpectationHandler>(services, operations, expectations, store);
        var context = new InventoryRequestContext<ProposeAccessExpectation>(request,
            ProgramManagementServices.Actor(fixture.ManagerUserId));
        Result<AccessExpectationView> replay = default;

        // Act
        var error = await Record.ExceptionAsync(async () => replay = await handler.HandleAsync(context, CancellationToken.None));
        var saved = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessExpectation(fixture.TenantId, first.ExpectationId));

        // Assert
        Assert.Null(error);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first, replay.Value);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(0, store.BlockedReads);
    }

    sealed class MissingSuccessorStore(IImmutableContentStore inner, ImmutableContentRef missing) : IImmutableContentStore
    {
        public int BlockedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference == missing)
            {
                BlockedReads++;
                throw new ImmutableContentUnavailableException("The later successor body is temporarily missing.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class LosingStore(IImmutableContentStore inner, Func<Task> admit) : IImmutableContentStore
    {
        bool _armed = true;
        public int CompetingAdmissions { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (_armed && schema == AccessReviewContent.ExpectationSchema)
            {
                _armed = false;
                await admit();
                CompetingAdmissions++;
                throw new ImmutableContentUnavailableException("The losing expectation candidate write was unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
