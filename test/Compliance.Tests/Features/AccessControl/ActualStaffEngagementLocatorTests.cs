using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ActualStaffEngagementLocatorTests
{
    [Fact]
    public async Task ShouldDiscoverActualStaffGivenRetainedAcceptance()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());

        // Act
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var page = await directory.ListAsync(fixture.Tenant, 200, null);

        // Assert
        var row = Assert.Single(page.Items);
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        Assert.Equal(fixture.Tenant, row.TenantId);
        Assert.Equal(fixture.Lead.StaffMemberId, row.StaffMemberId);
        Assert.Equal(fixture.Lead.UserId, row.UserId);
        Assert.Equal(accepted.OperationId, row.AcceptanceRequestId);
        Assert.Equal((long)accepted.Revision, row.AcceptanceSourceSequence);
        Assert.Equal(source.ResourceOffset, row.SourceResourceOffset);
        Assert.Equal(source.NextCursor.Value, row.SourceNextCursor);
        Assert.Equal(source.Stream.Realm, row.SourceRealm);
        Assert.Equal(source.Stream.Area, row.SourceArea);
        Assert.Equal(source.Stream.Resource, row.SourceResource);
        Assert.Equal(Uuid.Parse(source.Stream.Resource, CultureInfo.InvariantCulture), row.EngagementId);
        Assert.Equal(IndependenceSourceDigest.AcceptanceSource(accepted), row.SourcePayloadSha256);
        Assert.Equal(source.NextCursor, (await directory.LoadCheckpointAsync(fixture.Tenant)).Cursor);
        Assert.Null(page.NextCursor);
        Assert.Empty((await directory.ListAsync(Uuid.CreateVersion4(), 200, null)).Items);
    }

    [Fact]
    public async Task ShouldPageEveryPermanentLocatorGivenMoreThanTwoHundredRetainedAssignments()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 30, engagementCount: 7);
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var rows = new List<ActualStaffEngagementLocatorView>();
        string? cursor = null;

        // Act
        do
        {
            var page = await directory.ListAsync(fixture.Tenant, 200, cursor);
            rows.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        await fixture.ProjectAsync();

        // Assert
        Assert.Equal(210, rows.Count);
        Assert.Equal(210, rows.Select(row => row.LocatorId).Distinct().Count());
        Assert.Equal(7, (await directory.ListForStaffAsync(fixture.Tenant, fixture.Lead.StaffMemberId,
            fixture.Lead.UserId, 200, null)).Items.Count);
        Assert.Empty((await directory.ListForStaffAsync(fixture.Tenant, fixture.Lead.StaffMemberId,
            Uuid.CreateVersion4(), 200, null)).Items);
        Assert.Empty((await directory.ListForStaffAsync(Uuid.CreateVersion4(), fixture.Lead.StaffMemberId,
            fixture.Lead.UserId, 200, null)).Items);
    }

    [Fact]
    public async Task ShouldDiscoverOnlyTheAssignedStaffGivenLaterActualAssignmentOfTheAcceptedEngagement()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 1, engagementCount: 0);
        var (engagement, _, view) = await fixture.AcceptAsync(fixture.Tenant, [fixture.Lead]);
        var assignee = await fixture.AddStaffAsync();
        await ServiceEngagementFacts.AssignAsync(fixture.Provider, fixture.Tenant, engagement, view, assignee,
            ActorReference.ForFirmStaff(Uuid.CreateVersion4(), "Synthetic partner"), AcceptedStaffFixture.Now.AddMinutes(5));
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        var assigned = await directory.ListForStaffAsync(fixture.Tenant, assignee.StaffMemberId, assignee.UserId, 200, null);
        var rows = await directory.ListAsync(fixture.Tenant, 200, null);

        // Assert
        var row = Assert.Single(assigned.Items);
        Assert.Equal(engagement, row.EngagementId);
        Assert.Equal(2, row.AcceptanceRevision);
        Assert.Equal(3, row.AcceptanceSourceSequence);
        Assert.Equal(2, rows.Items.Count);
        Assert.Equal(rows.Items.Count, rows.Items.Select(item => item.LocatorId).Distinct().Count());
    }

    [Theory]
    [InlineData("accepted_snapshot")]
    [InlineData("source_intent")]
    public async Task ShouldKeepOriginalLocatorAndCheckpointGivenConflictingImmutableSourceReplay(string mutation)
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var before = await directory.LoadCheckpointAsync(fixture.Tenant);
        var original = Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        var view = await ReadAcceptanceAsync(fixture, accepted);
        var altered = source with
        {
            Event = mutation == "accepted_snapshot"
                ? accepted with
                {
                    OperationResult = await WriteAsync(fixture, view with { AuthorityReference = "Changed retained evidence" }),
                }
                : accepted with { Operation = accepted.Operation with { Episode = "different" } },
        };
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity, before)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await projection.ApplyAsync(altered, new Context(fixture.Identity), CancellationToken.None));

        // Assert
        Assert.Equal(before, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Equal(original, Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items));
    }

    [Fact]
    public async Task ShouldPreserveExactLocatorGivenIdenticalSourceRedelivery()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var checkpoint = await directory.LoadCheckpointAsync(fixture.Tenant);
        var original = Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity, checkpoint)))
        {
            await projection.ApplyAsync(source, new Context(fixture.Identity), CancellationToken.None);
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Equal(original, Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items));
    }

    [Fact]
    public async Task ShouldRetainPermanentLocatorsGivenClosureOfTheAcceptedEngagement()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 2);
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var before = await directory.ListAsync(fixture.Tenant, 200, null);
        var checkpoint = await directory.LoadCheckpointAsync(fixture.Tenant);
        var accepted = Assert.Single(await fixture.Services.GetRequiredService<ServiceEngagementViews>()
            .AcceptedAsync(fixture.Tenant));

        // Act
        await ServiceEngagementFacts.CloseAcceptedAsync(fixture.Provider, fixture.Tenant, accepted.Engagement.EngagementId,
            accepted.Acceptance, ActorReference.ForMember(RbacIds.Member(fixture.Tenant, Uuid.CreateVersion4()),
                "Synthetic client"), AcceptedStaffFixture.Now.AddMinutes(30));
        await fixture.ProjectAsync();

        // Assert
        Assert.Equal(before.Items.OrderBy(row => row.LocatorId.ToString()),
            (await directory.ListAsync(fixture.Tenant, 200, null)).Items.OrderBy(row => row.LocatorId.ToString()));
        Assert.NotEqual(checkpoint, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Equal(2, before.Items.Count);
    }

    [Fact]
    public async Task ShouldSeparateRetainedLocatorsGivenSameCanonicalStaffInTwoClients()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        var otherTenant = Uuid.CreateVersion4();
        await fixture.AcceptAsync(otherTenant, fixture.Staff);
        await fixture.ProjectAsync();
        await fixture.ProjectAsync(otherTenant);
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        var first = await directory.ListForStaffAsync(fixture.Tenant, fixture.Lead.StaffMemberId, fixture.Lead.UserId,
            200, null);
        var second = await directory.ListForStaffAsync(otherTenant, fixture.Lead.StaffMemberId, fixture.Lead.UserId,
            200, null);

        // Assert
        Assert.Equal(fixture.Tenant, Assert.Single(first.Items).TenantId);
        Assert.Equal(otherTenant, Assert.Single(second.Items).TenantId);
        Assert.NotEqual(first.Items[0].LocatorId, second.Items[0].LocatorId);
        Assert.NotEqual(first.Items[0].EngagementId, second.Items[0].EngagementId);
        Assert.NotEqual(first.Items[0].SourceRealm, second.Items[0].SourceRealm);
    }

    [Fact]
    public async Task ShouldRejectForeignContextBeforeRowsGivenOpenOwningTenantBatch()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var otherTenant = Uuid.CreateVersion4();
        var altered = source with
        {
            Stream = new EventStreamAddress(otherTenant.ToString(), ServiceEngagementIndependence.Area,
                source.Stream.Resource),
        };
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity,
                         ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await projection.ApplyAsync(altered, new Context(AcceptedStaffFixture.IdentityOf(otherTenant)),
                    CancellationToken.None));

        // Assert
        Assert.Empty((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        Assert.Empty((await directory.ListAsync(otherTenant, 200, null)).Items);
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(fixture.Tenant));
    }

    [Theory]
    [InlineData("foreign_stream")]
    [InlineData("wrong_resource")]
    [InlineData("foreign_snapshot")]
    [InlineData("empty_user")]
    [InlineData("invalid_practice")]
    [InlineData("invalid_revision")]
    [InlineData("duplicate_staff")]
    [InlineData("future_assignment")]
    [InlineData("missing_result")]
    public async Task ShouldRejectSourceBeforeRowsOrCheckpointGivenMalformedLocatorProvenance(string mutation)
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        var view = await ReadAcceptanceAsync(fixture, accepted);
        var assignment = Assert.Single(view.Assignments);
        async Task<DomainEventRecord> AlteredAsync() => mutation switch
        {
            "foreign_stream" => source with
            {
                Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(), ServiceEngagementIndependence.Area,
                    source.Stream.Resource),
            },
            "wrong_resource" => source with
            {
                Stream = new EventStreamAddress(fixture.Tenant.ToString(), ServiceEngagementIndependence.Area,
                    "not-an-engagement"),
            },
            "foreign_snapshot" => Replace(source, accepted, await WriteAsync(fixture,
                view with { TenantId = Uuid.CreateVersion4() })),
            "empty_user" => Replace(source, accepted, await WriteAsync(fixture,
                view with { Assignments = [assignment with { UserId = Uuid.Empty }] })),
            "invalid_practice" => Replace(source, accepted, await WriteAsync(fixture,
                view with { Assignments = [assignment with { Practice = "unknown" }] })),
            "invalid_revision" => Replace(source, accepted, await WriteAsync(fixture,
                view with { Assignments = [assignment with { DirectoryStaffRevision = 0 }] })),
            "duplicate_staff" => Replace(source, accepted, await WriteAsync(fixture,
                view with { Assignments = [assignment, assignment] })),
            "future_assignment" => Replace(source, accepted, await WriteAsync(fixture, view with
            {
                Assignments = [assignment with { AssignedAt = assignment.AssignedAt.AddMinutes(1) }],
                RecordedAt = view.RecordedAt,
            })),
            "missing_result" => source with { Event = accepted with { OperationResult = null } },
            _ => throw new InvalidOperationException(mutation),
        };
        var altered = await AlteredAsync();
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity,
                         ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await projection.ApplyAsync(altered, new Context(fixture.Identity), CancellationToken.None));

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Empty((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
    }

    static DomainEventRecord Replace(DomainEventRecord source, ServiceEngagementAcceptanceRecorded accepted,
        ImmutableContentRef result) => source with { Event = accepted with { OperationResult = result } };

    static async Task<ServiceEngagementAcceptanceView> ReadAcceptanceAsync(AcceptedStaffFixture fixture,
        ServiceEngagementAcceptanceRecorded accepted) => await IndependenceContent.ReadResultAsync<
        ServiceEngagementAcceptanceView>(fixture.Services.GetRequiredService<IImmutableContentStore>(),
        fixture.Tenant.ToString(), accepted.OperationResult!, CancellationToken.None);

    static async Task<ImmutableContentRef> WriteAsync(AcceptedStaffFixture fixture, ServiceEngagementAcceptanceView view)
    {
        var content = fixture.Services.GetRequiredService<IImmutableContentStore>();
        var realm = fixture.Tenant.ToString();
        var reference = await IndependenceContent.WriteAsync(content, realm, EngagementAcceptanceEffects.AcceptanceSchema,
            view, CancellationToken.None);
        return await IndependenceContent.WriteResultAsync(content, realm, EngagementAcceptanceEffects.ResultSchema,
            reference, CancellationToken.None);
    }

    sealed record Context(CheckpointIdentity Identity) : IProjectorContext
    {
        public bool IsRebuild => false;
    }
}
