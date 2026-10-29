using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class CampaignAcceptedRetryTests
{
    [Fact]
    public async Task ShouldReplayFrozenAcceptanceAndEnforceCurrentPersonalAuthorityGivenUnavailableAudienceBodies()
    {
        // Arrange
        var content = new AudienceFailure(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        await using var host = new CampaignHost(content: content);
        await host.SeedAsync();
        var campaignId = await host.LaunchAsync();
        var request = host.Acknowledge(campaignId, host.LearnerPerson, 1);
        var accepted = await host.SendAsync(request, host.Learner);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var before = await host.CampaignAsync(campaignId);
        content.BlockAudience = true;
        host.Clock.Advance(TimeSpan.FromHours(2));

        // Act
        var replay = await host.SendAsync(request, host.Learner);
        await ProgramManagementServices.SeedAsync(host.Provider, new PersonRules(host.Tenant, host.LearnerPerson), owner =>
        {
            Assert.Null(owner.CorrelateMembership(owner.Revision, Uuid.CreateVersion4(), host.ManagerActor, host.Clock.GetUtcNow()));
            return Result.Success;
        });
        var denied = await host.SendAsync(request, host.Learner);
        var after = await host.CampaignAsync(campaignId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(accepted.Value, replay.Value);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(0, content.BlockedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Single(after.Admitted);
        Assert.Empty(await host.EventsAsync<PolicyAcknowledgementRecorded>(
            PolicyCampaignParticipant.StreamOf(host.Tenant, campaignId, host.LearnerPerson)));
    }

    sealed class AudienceFailure(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool BlockAudience { get; set; }
        public int BlockedReads { get; private set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (BlockAudience && reference.Schema is PolicyCampaignContent.TeamsSchema or PolicyCampaignContent.EntriesSchema or
                PolicyCampaignContent.AmendmentsSchema)
            {
                BlockedReads++;
                throw new ImmutableContentUnavailableException("Current audience business bodies are unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
