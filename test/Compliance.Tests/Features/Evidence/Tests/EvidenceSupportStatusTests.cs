using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Handlers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceSupportStatusTests
{
    [Fact]
    public async Task ShouldRetainOriginalAssertionGivenWithdrawnCurrentStatus()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var program = Uuid.CreateVersion4();
        var link = await SeedAsync(fixture, program);
        var original = await GetAsync(fixture, program, link.CommandId);
        await WithdrawAsync(fixture, program, link.CommandId);

        // Act
        var result = await GetAsync(fixture, program, link.CommandId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(original.Value.IsActive);
        Assert.Null(original.Value.Withdrawal);
        Assert.Equal(original.Value.Original, result.Value.Original);
        Assert.False(result.Value.IsActive);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal("Wrong target", result.Value.Withdrawal!.Reason);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), result.Value.Withdrawal.ActorMemberId);
        Assert.Equal(ArtifactMetadataAccessTests.Fixture.Now, result.Value.Withdrawal.WithdrawnAt);
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("revoked")]
    [InlineData("suspended")]
    [InlineData("foreign_program")]
    public async Task ShouldHideStatusWithoutDisclosureGivenMissingCurrentAccess(string denied)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var grant = denied == "hidden" ? Uuid.Empty : await fixture.AllowAsync();
        var program = Uuid.CreateVersion4();
        var link = await SeedAsync(fixture, program);
        await WithdrawAsync(fixture, program, link.CommandId);
        if (denied is "revoked" or "suspended")
            await fixture.RevokeAuthorityAsync(denied, grant);

        // Act
        var result = await GetAsync(fixture, denied == "foreign_program" ? Uuid.CreateVersion4() : program,
            link.CommandId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldListActiveAndWithdrawnWithoutChangingOriginalGivenCurrentSource()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var program = Uuid.CreateVersion4();
        var first = await SeedAsync(fixture, program);
        var second = await SeedAsync(fixture, program);
        await WithdrawAsync(fixture, program, first.CommandId);

        // Act
        var all = await ListAsync(fixture, new(fixture.TenantId, program));
        var active = await ListAsync(fixture, new(fixture.TenantId, program, "active"));
        var withdrawn = await ListAsync(fixture, new(fixture.TenantId, program, "withdrawn"));

        // Assert
        Assert.True(all.IsSuccess, all.Error?.Message);
        Assert.Equal(2, all.Value.Items.Count);
        Assert.Equal(second.CommandId, Assert.Single(active.Value.Items).Original.EvidenceSupportId);
        var terminal = Assert.Single(withdrawn.Value.Items);
        Assert.Equal(first.CommandId, terminal.Original.EvidenceSupportId);
        Assert.False(terminal.IsActive);
        Assert.Equal(first.SupportedRecordId, terminal.Original.SupportedRecordId);
        Assert.Equal(fixture.ArtifactId, terminal.Original.ArtifactId);
    }

    [Fact]
    public async Task ShouldOmitHiddenReferencesGivenListWithoutArtifactDiscovery()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var program = Uuid.CreateVersion4();
        await SeedAsync(fixture, program);

        // Act
        var result = await ListAsync(fixture, new(fixture.TenantId, program));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Items);
        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task ShouldBindPageToScopeFilterAndSourceGivenContinuation()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var program = Uuid.CreateVersion4();
        var first = await SeedAsync(fixture, program);
        await SeedAsync(fixture, program);
        var page = await ListAsync(fixture, new(fixture.TenantId, program, Limit: 1));
        Assert.True(page.IsSuccess, page.Error?.Message);
        Assert.NotNull(page.Value.NextCursor);
        var continuation = new ListEvidenceSupports(fixture.TenantId, program, Limit: 1,
            Cursor: page.Value.NextCursor);

        // Act
        var next = await ListAsync(fixture, continuation);
        var otherProgram = await ListAsync(fixture, continuation with { ProgramId = Uuid.CreateVersion4() });
        var otherFilter = await ListAsync(fixture, continuation with { Status = "active" });
        await WithdrawAsync(fixture, program, first.CommandId);
        var changed = await ListAsync(fixture, continuation);

        // Assert
        Assert.True(next.IsSuccess, next.Error?.Message);
        Assert.Single(next.Value.Items);
        Assert.NotEqual(page.Value.Items[0].Original.EvidenceSupportId, next.Value.Items[0].Original.EvidenceSupportId);
        Assert.Equal(RequestErrorKind.Validation, otherProgram.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, otherFilter.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.True(changed.Error!.IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseStaleStatusGivenWithdrawalDuringAssembly(bool list)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var program = Uuid.CreateVersion4();
        var link = await SeedAsync(fixture, program);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var hooked = new AfterSupportRead(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            () => WithdrawAsync(fixture, program, link.CommandId));
        var read = Read(scope.ServiceProvider, hooked);
        var dispatch = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId));

        // Act
        var error = list
            ? (await new ListEvidenceSupportsHandler(read).HandleAsync(new RequestContext<ListEvidenceSupports>(
                new(fixture.TenantId, program), dispatch), CancellationToken.None)).Error
            : (await new GetEvidenceSupportHandler(read).HandleAsync(new RequestContext<GetEvidenceSupport>(
                new(fixture.TenantId, program, link.CommandId), dispatch), CancellationToken.None)).Error;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, error?.Kind);
        Assert.True(error!.IsTransient);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(EvidenceSupport.ForVerification(fixture.TenantId, link.CommandId));
        Assert.False(retained.IsActive);
        Assert.Equal(2, retained.Revision);
    }

    [Fact]
    public async Task ShouldPreserveFrozenLinkReadGivenCurrentStatusReadAfterWithdrawal()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var program = Uuid.CreateVersion4();
        var link = await SeedAsync(fixture, program);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reads = new EvidenceSupportReader(scope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var before = await reads.ReadOriginalAsync(fixture.TenantId, program, link.CommandId);
        await WithdrawAsync(fixture, program, link.CommandId);

        // Act
        var status = await reads.ReadStatusAsync(fixture.TenantId, program, link.CommandId);
        var after = await reads.ReadOriginalAsync(fixture.TenantId, program, link.CommandId);

        // Assert
        Assert.True(status.IsSuccess, status.Error?.Message);
        Assert.False(status.Value.IsActive);
        Assert.Equal(before.Value, status.Value.Original);
        Assert.Equal(before.Value, after.Value);
    }

    [Theory]
    [InlineData("bad", 1)]
    [InlineData(null, 0)]
    [InlineData(null, 201)]
    public async Task ShouldRefuseInvalidListGivenInvalidFilterOrLimit(string? status, int limit)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();

        // Act
        var result = await ListAsync(fixture, new(fixture.TenantId, Uuid.CreateVersion4(), status, limit));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    static Task WithdrawAsync(ArtifactMetadataAccessTests.Fixture fixture, Uuid program, Uuid support) =>
        ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceSupport(fixture.TenantId, program, support), owner =>
        {
            Assert.Null(owner.Withdraw(new(Uuid.CreateVersion4(), fixture.TenantId, program, support, 1, "Wrong target"),
                ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Lead"),
                ArtifactMetadataAccessTests.Fixture.Now));
            return Result.Success;
        });

    static async Task<LinkEvidenceSupport> SeedAsync(ArtifactMetadataAccessTests.Fixture fixture, Uuid program)
    {
        var link = new LinkEvidenceSupport(Uuid.CreateVersion4(), fixture.TenantId, program, fixture.ArtifactId,
            "control", Uuid.CreateVersion4(), 3, new(2026, 1, 1), new(2026, 12, 31), "Design", "Exact source", 0);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new EvidenceSupport(fixture.TenantId, program, link.CommandId), owner =>
            {
                Assert.Null(owner.Link(link, 2, new string('a', 64),
                    ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Lead"),
                    ArtifactMetadataAccessTests.Fixture.Now.AddHours(-1)));
                return Result.Success;
            });
        return link;
    }

    static async Task<Result<EvidenceSupportStatusView>> GetAsync(ArtifactMetadataAccessTests.Fixture fixture,
        Uuid program, Uuid support)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var read = Read(scope.ServiceProvider);
        return await new GetEvidenceSupportHandler(read).HandleAsync(new RequestContext<GetEvidenceSupport>(
            new(fixture.TenantId, program, support), new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId))),
            CancellationToken.None);
    }

    static async Task<Result<Page<EvidenceSupportStatusView>>> ListAsync(ArtifactMetadataAccessTests.Fixture fixture,
        ListEvidenceSupports query)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await new ListEvidenceSupportsHandler(Read(scope.ServiceProvider)).HandleAsync(
            new RequestContext<ListEvidenceSupports>(query,
                new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId))), CancellationToken.None);
    }

    static EvidenceSupportStatusRead Read(IServiceProvider services, IAggregateReader? reader = null) => new(
        reader ?? services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IDomainEventReader>(),
        services.GetRequiredService<EvidenceArtifactReadAccess>());

    sealed class AfterSupportRead(IAggregateReader inner, Func<Task> after) : IAggregateReader
    {
        bool _fired;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
            if (!_fired && owner is EvidenceSupport)
            {
                _fired = true;
                await after().ConfigureAwait(false);
            }
            return owner;
        }
    }
}
