using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentAssertionNativeTests
{
    [Fact]
    public async Task ShouldReadCompleteRejectedHistoryAndReplayOriginalResultsGivenLaterAcceptedSuccessor()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        ProposeRiskControlTreatment? first = null;
        for (var index = 0; index < 24; index++)
        {
            var proposed = Proposal(fixture, index * 2);
            first ??= proposed;
            await fixture.Scenario(fixture.AssessorUserId).When(proposed).ExpectSuccess();
            await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId,
                new ReviewRiskControlTreatment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
                    proposed.CommandId, index * 2 + 1, "reject", "Further support is required."));
        }
        var accepted = Proposal(fixture, 48);
        await fixture.Scenario(fixture.AssessorUserId).When(accepted).ExpectSuccess();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId,
            new ReviewRiskControlTreatment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
                accepted.CommandId, 49, "accept", "The exact historical proposal is independently supported."));
        var retire = new RetireRiskControlTreatment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, accepted.CommandId, 50, "Withdraw the previous assertion.");
        await fixture.Scenario(fixture.AssessorUserId).When(retire).ExpectSuccess();
        var successor = Proposal(fixture, 51);
        await fixture.Scenario(fixture.AssessorUserId).When(successor).ExpectSuccess();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId,
            new ReviewRiskControlTreatment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
                successor.CommandId, 52, "accept", "The successor is independently supported."));
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var history = scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>();

        // Act
        var replay = await fixture.Scenario(fixture.AssessorUserId).When(first!).ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId).When(retire).ExpectSuccess();
        var owner = await reader.HydrateAsync(new RiskTreatmentAssertion(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, fixture.ControlId));
        var facts = await RiskTreatmentAssertionHistory.ReadAsync(events, owner);
        var views = await history.ComposeTreatmentsAsync(fixture.TenantId, owner);

        // Assert
        Assert.Equal(first!.CommandId, replay.Value.TreatmentId);
        Assert.Equal(1, replay.Value.Revision);
        Assert.Equal(53, owner.Revision);
        Assert.Equal(53, facts.Count);
        Assert.Equal(26, views.Count);
        Assert.Equal(24, views.Count(view => view.Status == "rejected"));
        Assert.Equal("retired", views.Single(view => view.TreatmentId == accepted.CommandId).Status);
        Assert.Equal(successor.CommandId, owner.Accepted!.TreatmentId);
        Assert.Null(owner.Pending);
        Assert.Equal(facts[^1].Metadata.EventId, owner.Latest!.Metadata.EventId);
    }

    [Fact]
    public async Task ShouldReplayAcceptedReviewWithoutReadingProposalBodyGivenImmutableNarrativeUnavailable()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var proposed = Proposal(fixture, 0);
        await fixture.Scenario(fixture.AssessorUserId).When(proposed).ExpectSuccess();
        var review = new ReviewRiskControlTreatment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
            proposed.CommandId, 1, "accept", "Approved independently.");
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId, review);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = new UnavailableProposalContent(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        var history = new RiskGovernanceReader(reader, scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), content);
        var handler = new ReviewRiskControlTreatmentHandler(new NativeOperationExecutor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content), reader, history,
            scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Independence.Contracts.ISeparationOfDutiesWaiverProofResolver>(), content, TimeProvider.System);
        var context = new Cntryl.Portia.RequestContext<ReviewRiskControlTreatment>(review,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ApproverUserId),
                new HttpInvocation("PUT", "/api/v1/assertion-review", "/api/v1/assertion-review", "assertion-review")));

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(0, content.ProposalReads);
        Assert.Equal(2, (await reader.HydrateAsync(new RiskTreatmentAssertion(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, fixture.ControlId))).Revision);
    }

    static ProposeRiskControlTreatment Proposal(RiskGovernanceHandlerTests.Fixture fixture, long revision) => new(Uuid.CreateVersion4(),
        fixture.TenantId, fixture.ProgramId, fixture.RiskId, revision, fixture.ControlId, fixture.ControlVersionId, "The control treats the exact risk.");

    sealed class UnavailableProposalContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int ProposalReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema == RiskContent.TreatmentSchema)
            {
                ProposalReads++;
                throw new ImmutableContentUnavailableException("The immutable proposal is temporarily unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
