using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Risks.Services;
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
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskMethodPrepareFailureTests
{
    [Fact]
    public async Task ShouldReplayEqualWinnerGivenPreparedContentFailureAfterConcurrentAdmission()
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        var request = fixture.Request(0, 12);
        RiskMethodVersionView? winner = null;
        var content = new ConcurrentAdmissionContent(fixture.Content, async () =>
        {
            var accepted = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
            fixture.Clock.Now = fixture.Clock.Now.AddDays(2);
        });
        var handler = new PublishRiskMethodVersionHandler(new NativeOperationExecutor(fixture.Reader, fixture.Executor,
            content), fixture.Reader, content, fixture.Clock);

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(winner);
        Assert.Equal(winner.MethodVersionId, result.Value!.MethodVersionId);
        Assert.Equal(winner.PublishedAt, result.Value.PublishedAt);
        Assert.Equal(1, (await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program))).PublishedVersion);
    }

    sealed class ConcurrentAdmissionContent(IImmutableContentStore inner, Func<Task> admit) : IImmutableContentStore
    {
        bool _armed = true;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Cntryl.Portia.Uuid id, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (_armed && schema == RiskMethodContent.Schema)
            {
                _armed = false;
                await admit();
                throw new ImmutableContentUnavailableException("The prepared write lost its response after another equal request committed.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
