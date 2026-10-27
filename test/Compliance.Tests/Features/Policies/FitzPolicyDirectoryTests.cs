using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Handlers;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Policies;

public sealed class FitzPolicyDirectoryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    static PolicyContent Content(string title, int cadence = 12) => new(title, "Purpose",
        PolicyAudience.CoreSecurity, null, cadence, "Body", null, "Owner", []);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId, "Member");

    [Fact]
    public async Task ShouldReadOnlyMatchingCampaignsGivenLargeUnrelatedProgramPopulation()
    {
        // Arrange
        var client = new CountingKvClient(new InMemoryKvClient());
        var directory = new FitzCampaignDirectory(client);
        var policyId = Uuid.CreateVersion4();
        var matches = new[] { Launched(policyId, 1, Now), Launched(policyId, 1, Now.AddDays(1)) };
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzCampaignDirectory.ProjectorName,
                             EventStreamPattern.ForPattern(TenantId.ToString(),
                                 "policy-distribution-campaigns")), ProjectionCheckpoint.Start)))
        {
            foreach (var campaign in matches)
                await directory.ApplyAsync(campaign);
            for (var i = 0; i < 405; i++)
                await directory.ApplyAsync(Launched(Uuid.CreateVersion4(), 1, Now.AddMinutes(i)));
            await directory.ApplyAsync(Launched(policyId, 2, Now));
            await directory.ApplyAsync(Launched(policyId, 1, Now) with { ProgramId = Uuid.CreateVersion4() });
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        client.ScannedRows = 0;

        // Act
        var found = await directory.ListForSubjectAsync(TenantId, ProgramId, policyId, 1);

        // Assert
        Assert.Equal(matches.Select(static campaign => campaign.CampaignId).Order(), found.Order());
        Assert.Equal(matches.Length, client.ScannedRows);
        Assert.Empty(await directory.ListForSubjectAsync(Uuid.CreateVersion4(), ProgramId, policyId, 1));
    }

    [Fact]
    public async Task ShouldProjectLifecycleAndDeleteDiscardedDraftGivenPolicyEvents()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var directory = new FitzPolicyDirectory(new InMemoryKvClient(), source.Policies);
        var policy = new PolicyRules(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-B"));
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-B", Content("Draft"),
            Actor(AuthorId), AuthorId, Now).IsSuccess);
        Assert.Null(policy.Revise(ProgramId, 1, Content("Reviewed", 6), Actor(AuthorId), AuthorId,
            Now));
        var review = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, 2, review, "accept", "Ok", Actor(ReviewerId),
            ReviewerId, Now));
        Assert.Null(policy.Approve(ProgramId, 2, Uuid.CreateVersion4(), review,
            new DateOnly(2026, 10, 1), true, "Ok", null, Actor(ApproverId), ApproverId, Now));
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("Successor"), Actor(AuthorId),
            AuthorId, Now));
        var unused = new PolicyRules(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-A"));
        Assert.True(unused.Create(ProgramId, Uuid.CreateVersion4(), "POL-A", Content("Unused"),
            Actor(AuthorId), AuthorId, Now).IsSuccess);
        Assert.Null(unused.Discard(ProgramId, 1, "Not needed", Actor(AuthorId), Now));
        var other = new Policy(TenantId, Uuid.CreateVersion4());
        await source.SeedAsync(policy);
        await source.SeedAsync(unused);
        var policyFacts = await source.FactsAsync(TenantId, policy.Id);
        var discardedFacts = await source.FactsAsync(TenantId, unused.Id);
        var malformed = await new PolicyProjectionFacts().FreezeAsync(new PolicyDraftDiscardedContent(TenantId,
            ProgramId, other.Id, 1, "Out of order", Actor(AuthorId), Now));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(TenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var ev in policyFacts.Concat(discardedFacts))
                await directory.ApplyAsync(ev);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(malformed));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(TenantId, ProgramId, 50, null);
        var alien = await directory.ListProgramAsync(Uuid.CreateVersion4(), ProgramId, 50, null);

        // Assert
        var row = Assert.Single(page.Items);
        Assert.Equal("POL-B", row.Identifier);
        Assert.Equal("Reviewed", row.Title);
        Assert.Equal("approved", row.Status);
        Assert.Equal("draft", row.PendingStatus);
        Assert.Equal(1, row.CurrentVersion);
        Assert.Equal(3, row.Revision);
        Assert.Equal(new DateOnly(2027, 3, 30), row.NextReviewDueOn);
        Assert.Empty(alien.Items);
    }

    [Fact]
    public async Task ShouldListRetiredSummaryAndKeepVersionHistoryGivenSuccessorRetirement()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var directory = new FitzPolicyDirectory(new InMemoryKvClient(), source.Policies);
        var policy = new PolicyRules(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-HISTORY"));
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-HISTORY",
            Content("Initial"), Actor(AuthorId), AuthorId, Now).IsSuccess);
        var firstReview = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, policy.Revision, firstReview, "accept", "Reviewed",
            Actor(ReviewerId), ReviewerId, Now.AddMinutes(1)));
        Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), firstReview,
            new DateOnly(2026, 10, 1), true, "Approved", null, Actor(ApproverId), ApproverId,
            Now.AddMinutes(2)));
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("Successor"),
            Actor(AuthorId), AuthorId, Now.AddMinutes(3)));
        var successorReview = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, policy.Revision, successorReview, "accept",
            "Reviewed", Actor(ReviewerId), ReviewerId, Now.AddMinutes(4)));
        Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            successorReview, new DateOnly(2027, 1, 1), false, "Approved successor",
            "impact-digest", Actor(ApproverId), ApproverId, Now.AddMinutes(5)));
        Assert.Null(policy.ProposeRetirement(ProgramId, 2, new DateOnly(2027, 6, 1),
            "Policy retired", Actor(AuthorId), AuthorId, Now.AddMinutes(6)));
        var retirementReview = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, policy.Revision, retirementReview, "accept",
            "Reviewed", Actor(ReviewerId), ReviewerId, Now.AddMinutes(7)));
        Assert.Null(policy.ApproveRetirement(ProgramId, policy.Revision, Uuid.CreateVersion4(),
            retirementReview, "Approved retirement", Actor(ApproverId), ApproverId,
            Now.AddMinutes(8)));

        // Act
        await source.SeedAsync(policy);
        var events = await source.FactsAsync(TenantId, policy.Id);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(TenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var ev in events)
                await directory.ApplyAsync(ev);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(TenantId, ProgramId, 50, null);
        var replayedPolicy = new AggregateScenario<Policy>(new Policy(TenantId, policy.Id))
            .Given(events.Select((ev, index) => DomainEventSeed.Attach(RoundTrip(ev), policy.Id,
                (ulong)index + 1)).ToArray()).Aggregate;
        var reader = new PolicyReader(new PolicyAggregateReader(replayedPolicy), source.Events, source.Content);
        var versions = await new ListPolicyVersionsHandler(reader).HandleAsync(
            new RequestContext<ListPolicyVersions>(new ListPolicyVersions(TenantId, ProgramId,
                policy.Id), new ClaimsPrincipal()), CancellationToken.None);
        var effective = await new GetEffectivePolicyVersionHandler(reader).HandleAsync(
            new RequestContext<GetEffectivePolicyVersion>(new GetEffectivePolicyVersion(TenantId,
                ProgramId, policy.Id, new DateOnly(2026, 12, 31)), new ClaimsPrincipal()),
            CancellationToken.None);
        var effectiveSuccessor = await new GetEffectivePolicyVersionHandler(reader).HandleAsync(
            new RequestContext<GetEffectivePolicyVersion>(new GetEffectivePolicyVersion(TenantId,
                ProgramId, policy.Id, new DateOnly(2027, 1, 1)), new ClaimsPrincipal()),
            CancellationToken.None);
        var afterRetirement = await new GetEffectivePolicyVersionHandler(reader).HandleAsync(
            new RequestContext<GetEffectivePolicyVersion>(new GetEffectivePolicyVersion(TenantId,
                ProgramId, policy.Id, new DateOnly(2027, 6, 1)), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        var summary = Assert.Single(page.Items);
        Assert.Equal("retired", summary.Status);
        Assert.Null(summary.PendingStatus);
        Assert.Equal(2, summary.CurrentVersion);
        Assert.Null(summary.NextReviewDueOn);
        Assert.True(versions.IsSuccess);
        Assert.Equal(["superseded", "retired"],
            versions.Value.Items.Select(static version => version.Status));
        Assert.True(effective.IsSuccess);
        Assert.Equal(1, effective.Value.Version);
        Assert.True(effectiveSuccessor.IsSuccess);
        Assert.Equal(2, effectiveSuccessor.Value.Version);
        Assert.Equal(RequestErrorKind.NotFound, afterRetirement.Error!.Kind);
    }

    [Fact]
    public async Task ShouldListCampaignsBoundToExactVersionGivenLaunchedAndClosedCampaigns()
    {
        // Arrange
        var directory = new FitzCampaignDirectory(new InMemoryKvClient());
        var policyId = Uuid.CreateVersion4();
        var first = Launched(policyId, 1, Now);
        var second = Launched(policyId, 2, Now.AddDays(1));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzCampaignDirectory.ProjectorName,
                             EventStreamPattern.ForPattern(TenantId.ToString(),
                                 "policy-distribution-campaigns")), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await directory.ApplyAsync(CampaignFacts.Closed(TenantId, first.CampaignId,
                new DateOnly(2026, 11, 1), "Done", Actor(AuthorId), Now.AddDays(30)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(TenantId, ProgramId, 50, null);
        var boundToFirst = await directory.ListForSubjectAsync(TenantId, ProgramId, policyId, 1);

        // Assert
        Assert.Equal([first.CampaignId, second.CampaignId],
            page.Items.Select(static campaign => campaign.CampaignId));
        Assert.Equal("closed", page.Items[0].Status);
        Assert.Equal("open", page.Items[1].Status);
        Assert.Equal([first.CampaignId], boundToFirst);
    }

    static PolicyCampaignLaunched Launched(Uuid policyId, long version, DateTimeOffset at)
    {
        var campaignId = Uuid.CreateVersion4();
        var stream = new PolicyDistributionCampaign(TenantId, campaignId).Stream;
        return new PolicyCampaignLaunched(TenantId, ProgramId, campaignId,
            new CampaignSubject("policy", policyId, "POL-B", "Policy", version, "sha"),
            PolicyAudience.CoreSecurity, CampaignFacts.Opaque(), Uuid.CreateVersion4(), "roster",
            DateOnly.FromDateTime(at.UtcDateTime), DateOnly.FromDateTime(at.UtcDateTime).AddDays(30),
            null, PolicyDistributionCampaign.DefaultAcknowledgementText, 3, Actor(AuthorId),
            AuthorId, at, CampaignFacts.Provenance(campaignId, "LaunchPolicyCampaign", stream, Actor(AuthorId), at),
            CampaignFacts.Opaque(), CampaignFacts.Opaque());
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzPolicyDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "policies"));

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default), ev.GetType(),
        ComplianceCoreJsonContext.Default)!;

    sealed class PolicyAggregateReader(Policy policy) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(policy is TAggregate typed ? typed : aggregate);
    }

    sealed class CountingKvClient(IKvClient inner) : IKvClient
    {
        public int ScannedRows { get; set; }

        public async Task<IKvTransaction> BeginAsync(string route,
            KvMode mode = KvMode.ReadWrite, CancellationToken ct = default) =>
            new CountingTransaction(await inner.BeginAsync(route, mode, ct), this);

        public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
            inner.SubscribeAsync(pattern, ct);

        sealed class CountingTransaction(IKvTransaction inner, CountingKvClient owner) : IKvTransaction
        {
            public string Route => inner.Route;
            public Task<KvGetResult> GetAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.GetAsync(key, ct);
            public Task PutAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) => inner.PutAsync(key, value, ct);
            public Task InsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) => inner.InsertAsync(key, value, ct);
            public Task DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.DeleteAsync(key, ct);
            public Task DeleteRangeAsync(ReadOnlyMemory<byte> startKey, ReadOnlyMemory<byte> endKey,
                CancellationToken ct = default) => inner.DeleteRangeAsync(startKey, endKey, ct);
            public async Task<KvScanResult> ScanAsync(KvScanQuery query, CancellationToken ct = default)
            {
                var result = await inner.ScanAsync(query, ct);
                owner.ScannedRows += result.Pairs.Count;
                return result;
            }
            public Task CommitAsync(KvDurability durability, CancellationToken ct = default) =>
                inner.CommitAsync(durability, ct);
            public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
