using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Services;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Projectors;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class UserIdentityObservationTests
{
    [Fact]
    public async Task ShouldRetainExactRegistrationProvenanceGivenAnAdmittedProfileAudit()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        await using var serializers = EventPersistenceFrameTests.Compose();
        var serializer = serializers.GetRequiredService<IDomainEventSerializer>();
        var context = Context();
        var identity = new UserIdentity("issuer", "subject");
        var userId = Uuid.CreateVersion4();
        Assert.True(identity.Register(userId, null).IsSuccess);
        await fixture.Repository.SaveAsync(identity, context);
        var registration = Assert.IsType<UserIdentityRegistered>((await Records(fixture.Store))[0].Event);
        identity = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));

        // Act
        Assert.True(identity.ObserveAuthenticatedProfile("  Ada  ", DateTimeOffset.UtcNow).IsSuccess);
        await fixture.Repository.SaveAsync(identity, context);
        var record = Assert.Single(await Records(fixture.Store), record => record.Event is UserIdentityProfileObserved);
        var observed = Assert.IsType<UserIdentityProfileObserved>(serializer.Deserialize(serializer.Serialize(record.Event)));

        // Assert
        Assert.Equal("Ada", observed.DisplayName);
        Assert.Equal(userId, observed.UserId);
        Assert.True(observed.Metadata.IsAudit);
        Assert.Equal(identity.Id, observed.Metadata.AggregateId);
        Assert.NotEqual(identity.Stream, record.Stream);
        Assert.Equal(0UL, record.ResourceOffset);
        Assert.Equal(identity.Id, observed.Source.IdentityId);
        Assert.Equal(registration.Metadata.EventId, observed.Source.EventId);
        Assert.Equal(1UL, observed.Source.Revision);
        Assert.Equal(0UL, observed.Source.Position);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(registration,
            ComplianceCoreJsonContext.Default.UserIdentityRegistered))), observed.Source.Sha256);
        Assert.Equal(context.CauseId, observed.Metadata.CausationId);
        Assert.Equal(context.ExecutionId, observed.Metadata.ExecutionId);
        Assert.Equal(registration.Metadata.Actor, observed.Metadata.Actor);
    }

    [Fact]
    public async Task ShouldPersistHistoricalAdmissionWithoutRestoringAuthorityGivenRevocationBeforeAuditAppend()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var context = Context();
        var identity = new UserIdentity("issuer", "subject");
        Assert.True(identity.Register(Uuid.CreateVersion4(), null).IsSuccess);
        await fixture.Repository.SaveAsync(identity, context);
        var observation = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        Assert.True(observation.ObserveAuthenticatedProfile("Admitted before revocation", DateTimeOffset.UtcNow).IsSuccess);
        var retirement = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));

        // Act
        Assert.True(retirement.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow).IsSuccess);
        await fixture.Repository.SaveAsync(retirement, context);
        await fixture.Repository.SaveAsync(observation, context);
        var current = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        var records = await Records(fixture.Store);
        var admitted = Assert.IsType<UserIdentityProfileObserved>(records[^1].Event);

        // Assert
        Assert.IsType<UserIdentityRevoked>(records[^2].Event);
        Assert.True(current.IsRevoked);
        Assert.False(current.Authenticate().IsSuccess);
        Assert.Equal(2UL, current.Version);
        Assert.Equal(2UL, current.CommittedStreamPosition);
        Assert.Equal(1UL, admitted.Metadata.AggregateVersion);
        Assert.Equal(1UL, admitted.Source.Revision);
        Assert.NotEqual(retirement.Stream, records[^1].Stream);
    }

    [Fact]
    public async Task ShouldRejectProfileAdmissionGivenRevocationAlreadyCommitted()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var context = Context();
        var identity = new UserIdentity("issuer", "subject");
        Assert.True(identity.Register(Uuid.CreateVersion4(), null).IsSuccess);
        Assert.True(identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow).IsSuccess);
        await fixture.Repository.SaveAsync(identity, context);
        var continuation = new UserIdentityContinuation(fixture.Repository);

        // Act
        var result = await continuation.ContinueAsync("issuer", "subject", null, null,
            context, CancellationToken.None, observeProfile: true, displayName: "Rejected");
        var records = await Records(fixture.Store);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
        Assert.Equal(2, records.Count);
        Assert.DoesNotContain(records, record => record.Event is UserIdentityProfileObserved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepAssociationAndReplayStableGivenUnknownProfileAppendOutcome(bool committed)
    {
        // Arrange
        var store = new InterruptedObservationStore(committed);
        await using var fixture = new StoreFixture(store);
        var context = Context();
        var identity = new UserIdentity("issuer", "subject");
        var continuation = new UserIdentityContinuation(fixture.Repository);

        // Act
        await Assert.ThrowsAsync<IOException>(() => continuation.ContinueAsync("issuer", "subject",
            null, null, context, CancellationToken.None, observeProfile: true, displayName: "Ada").AsTask());
        var retry = await continuation.ContinueAsync("issuer", "subject", null, null,
            context, CancellationToken.None, observeProfile: true, displayName: "Ada");
        var current = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        var records = await Records(store);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(retry.Value.UserId, current.UserId);
        Assert.Equal(1UL, current.Version);
        Assert.Equal(1UL, current.CommittedStreamPosition);
        Assert.Single(records, record => record.Event is UserIdentityRegistered);
        Assert.Equal(committed ? 2 : 1, records.Count(record => record.Event is UserIdentityProfileObserved));
        Assert.All(records.Where(record => record.Event is UserIdentityProfileObserved), record =>
        {
            Assert.True(record.Event.Metadata.IsAudit);
            Assert.NotEqual(identity.Stream, record.Stream);
        });
    }

    [Fact]
    public async Task ShouldKeepNewerNameRemovalGivenAnEarlierAdmissionAppendsLate()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var context = Context();
        var identity = new UserIdentity("issuer", "subject");
        var userId = Uuid.CreateVersion4();
        Assert.True(identity.Register(userId, null).IsSuccess);
        await fixture.Repository.SaveAsync(identity, context);
        var earlier = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        var later = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        var now = DateTimeOffset.UtcNow;
        Assert.True(earlier.ObserveAuthenticatedProfile("Old provider name", now).IsSuccess);
        Assert.True(later.ObserveAuthenticatedProfile(null, now.AddSeconds(1)).IsSuccess);
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var projector = new PlatformUserDirectoryProjector(directory);

        // Act
        await fixture.Repository.SaveAsync(later, context);
        await fixture.Repository.SaveAsync(earlier, context);
        await new ProjectorRunner(fixture.Store).RunAsync(projector, ProjectionCheckpoint.Start);

        // Assert
        Assert.Null(await directory.ReadAsync(userId));
        Assert.True(await directory.ExistsAsync(userId));
    }

    [Fact]
    public async Task ShouldKeepSamePresentationGivenEqualAdmissionTimesReplayedInEitherOrder()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var context = Context();
        var identity = new UserIdentity("issuer", "subject");
        var userId = Uuid.CreateVersion4();
        Assert.True(identity.Register(userId, null).IsSuccess);
        await fixture.Repository.SaveAsync(identity, context);
        var earlier = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        var later = await fixture.Repository.HydrateAsync(new UserIdentity(identity.Id));
        var now = DateTimeOffset.UtcNow;
        Assert.True(earlier.ObserveAuthenticatedProfile("Ada", now).IsSuccess);
        Assert.True(later.ObserveAuthenticatedProfile(null, now).IsSuccess);
        await fixture.Repository.SaveAsync(earlier, context);
        await fixture.Repository.SaveAsync(later, context);
        var records = await Records(fixture.Store);
        var registration = records[0].Event;
        var observations = records.Select(record => record.Event).OfType<UserIdentityProfileObserved>()
            .OrderBy(observed => observed.Metadata.EventId.ToString(), StringComparer.Ordinal).ToArray();
        var forward = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var reverse = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var checkpoint = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await forward.BeginAsync(new ProjectionBatchContext(checkpoint,
                         ProjectionCheckpoint.Start)))
        {
            await forward.ApplyAsync(Assert.IsType<UserIdentityRegistered>(registration));
            foreach (var observation in observations)
                await forward.ApplyAsync(observation);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var batch = await reverse.BeginAsync(new ProjectionBatchContext(checkpoint,
                         ProjectionCheckpoint.Start)))
        {
            await reverse.ApplyAsync(Assert.IsType<UserIdentityRegistered>(registration));
            foreach (var observation in observations.Reverse())
                await reverse.ApplyAsync(observation);
            await reverse.ApplyAsync(observations[^1]);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal(observations[^1].DisplayName, await forward.ReadAsync(userId));
        Assert.Equal(await forward.ReadAsync(userId), await reverse.ReadAsync(userId));
    }

    [Fact]
    public async Task ShouldRebuildCurrentPresentationGivenRegistrationAndAuditSessionsShareASourceCursor()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var context = Context();
        var continuation = new UserIdentityContinuation(fixture.Repository);
        var first = await continuation.ContinueAsync("issuer", "subject", null, null, context,
            CancellationToken.None, observeProfile: true, displayName: "Ada");
        Assert.True(first.IsSuccess);
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var projector = new PlatformUserDirectoryProjector(directory);
        var runner = new ProjectorRunner(fixture.Store);
        var firstCheckpoint = await runner.RunAsync(projector, ProjectionCheckpoint.Start);

        // Act
        Assert.True((await continuation.ContinueAsync("issuer", "subject", null, null, context,
            CancellationToken.None, observeProfile: true, displayName: "Ada King")).IsSuccess);
        var secondCheckpoint = await runner.RunAsync(projector, firstCheckpoint);
        var renamed = await directory.ReadAsync(first.Value.UserId);
        Assert.True((await continuation.ContinueAsync("issuer", "subject", null, null, context,
            CancellationToken.None, observeProfile: true, displayName: null)).IsSuccess);
        var finalCheckpoint = await runner.RunAsync(projector, secondCheckpoint);
        var rebuilt = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var rebuiltCheckpoint = await runner.RunAsync(new PlatformUserDirectoryProjector(rebuilt),
            ProjectionCheckpoint.Start);

        // Assert
        Assert.NotEqual(firstCheckpoint, secondCheckpoint);
        Assert.Equal("Ada King", renamed);
        Assert.Equal(finalCheckpoint, rebuiltCheckpoint);
        Assert.Equal(finalCheckpoint, await runner.RunAsync(projector, finalCheckpoint));
        Assert.Null(await directory.ReadAsync(first.Value.UserId));
        Assert.Null(await rebuilt.ReadAsync(first.Value.UserId));
        Assert.True(await directory.ExistsAsync(first.Value.UserId));
        Assert.True(await rebuilt.ExistsAsync(first.Value.UserId));
    }

    static RequestContext<ContinueWithOidcProvider> Context() => new(new ContinueWithOidcProvider(),
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "issuer"), new Claim("sub", "subject")], "oidc")));

    static async Task<List<DomainEventRecord>> Records(IEventStore store)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var record in store.ReadAsync(EventStreamPattern.ForPattern("bdgrz", "user-identities"),
                           EventCursor.Start, CancellationToken.None))
            records.Add(record);
        return records;
    }

    sealed class InterruptedObservationStore(bool commitBeforeFailure) : IEventStore
    {
        readonly InMemoryEventStore _inner = new();
        bool _interrupted;
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            CancellationToken ct) => _inner.ReadAsync(stream, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct) => _inner.ReadAsync(pattern, cursor, ct);
        public async ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            if (!_interrupted && events.Any(ev => ev is UserIdentityProfileObserved))
            {
                _interrupted = true;
                if (commitBeforeFailure)
                    await _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
                throw new IOException("Profile audit outcome is unknown.");
            }
            await _inner.AppendAsync(stream, expectedStreamPosition, events, ct);
        }
    }
}
