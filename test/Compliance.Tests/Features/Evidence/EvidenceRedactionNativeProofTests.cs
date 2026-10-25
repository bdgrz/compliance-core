using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed partial class EvidenceRedactionCompositionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyExactNativeSourceManifestFailureWithoutAppendGivenQualifiedPreparation(bool missing)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, fixture.UserId, request)).IsSuccess);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);
        var first = Assert.IsType<EvidenceRedactionPreparedV2>(Assert.Single(history.Facts));
        var unavailable = new SourceContentFault(fixture.Provider.GetRequiredService<IImmutableContentStore>(),
            first.SourceProofs, missing, null);
        await using var provider = Compose(fixture, new MutableClock { Now = ArtifactMetadataAccessTests.Fixture.Now },
            Uuid.Empty, immutable: unavailable);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new GetEvidenceRedaction(fixture.TenantId, request.RedactionId),
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId), new DirectInvocation()));
        var after = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(missing, result.Error.IsTransient);
        Assert.True(unavailable.Reads > 0);
        Assert.Single(after.Facts);
        Assert.Equal(first.Metadata.EventId, after.Facts[0].Metadata.EventId);
    }

    [Fact]
    public async Task ShouldPropagateRequestedCancellationGivenActualNativeSourceManifestReadCancels()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, fixture.UserId, request)).IsSuccess);
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);
        var first = Assert.IsType<EvidenceRedactionPreparedV2>(Assert.Single(history.Facts));
        using var cancelled = new CancellationTokenSource();
        var content = new SourceContentFault(fixture.Provider.GetRequiredService<IImmutableContentStore>(), first.SourceProofs,
            true, cancelled);
        await using var provider = Compose(fixture, new MutableClock { Now = ArtifactMetadataAccessTests.Fixture.Now },
            Uuid.Empty, immutable: content);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
                new GetEvidenceRedaction(fixture.TenantId, request.RedactionId),
                new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId), new DirectInvocation()), cancelled.Token));

        // Assert
        Assert.True(cancelled.IsCancellationRequested);
        Assert.Equal(cancelled.Token, error.CancellationToken);
        Assert.True(content.Reads > 0);
        Assert.Single((await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId)).Facts);
    }

    [Fact]
    public async Task ShouldReplayActualEqualWinnerGivenNativeSourceCandidateWriteFailsAfterWinnerCommits()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        EvidenceRedactionView? winner = null;
        var content = new EqualWinnerContent(fixture.Provider.GetRequiredService<IImmutableContentStore>(), async () =>
        {
            var accepted = await DispatchAsync(fixture, fixture.UserId, request);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
        });
        await using var provider = Compose(fixture, new MutableClock { Now = ArtifactMetadataAccessTests.Fixture.Now },
            Uuid.Empty, immutable: content);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId),
                new HttpInvocation("PUT", "/test", "/test", "test")));
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, request.RedactionId);

        // Assert
        Assert.True(content.Triggered);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(winner);
        Assert.Equal(RedactionWire(winner!), RedactionWire(result.Value));
        Assert.Single(history.Facts);
        Assert.IsType<EvidenceRedactionPreparedV2>(history.Facts[0]);
    }

    [Fact]
    public async Task ShouldRefuseExpiredWaiverAtActualAppendGivenOriginalResponseFreezeCrossesItsDeadline()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        var approver = await SeedMemberAsync(fixture, BuiltInRbac.AdministratorsTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, lead, derived);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, approver, derived);
        var preparation = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");
        Assert.True((await DispatchAsync(fixture, lead, preparation)).IsSuccess);
        var recorded = await DispatchAsync(fixture, fixture.UserId, new RecordSeparationOfDutiesWaiver(fixture.TenantId,
            new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.EvidenceRedaction, preparation.RedactionId,
                preparation.CommandId, 1, SeparationOfDutiesActions.Approve), lead,
            "Exact independent exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1)));
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var waived = await DispatchAsync(fixture, approver, new ApproveSeparationOfDutiesWaiver(fixture.TenantId, recorded.Value.WaiverId));
        Assert.True(waived.IsSuccess, waived.Error?.Message);
        var clock = new MutableClock { Now = ArtifactMetadataAccessTests.Fixture.Now };
        var content = new DeadlineContent(fixture.Provider.GetRequiredService<IImmutableContentStore>(), clock);
        await using var provider = Compose(fixture, clock, Uuid.Empty, immutable: content);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ApproveEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, preparation.RedactionId, 1,
                preparation.CommandId, 1, recorded.Value.WaiverId),
            new RequestDispatchContext(ProgramManagementServices.Actor(lead), new HttpInvocation("PUT", "/test", "/test", "test")));
        var history = await ReadHistoryAsync(fixture.Provider, fixture.TenantId, preparation.RedactionId);

        // Assert
        Assert.True(content.Advanced);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Empty(history.Approvals);
        Assert.Single(history.Facts);
    }

    sealed class DeadlineContent(IImmutableContentStore inner, MutableClock clock) : IImmutableContentStore
    {
        public bool Advanced { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (schema == EvidenceRedactionOperations.ResultSchema)
            {
                Advanced = true;
                clock.Now = ArtifactMetadataAccessTests.Fixture.Now.AddHours(2);
            }
            return inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class SourceContentFault(IImmutableContentStore inner, ImmutableContentRef selected, bool missing,
        CancellationTokenSource? cancelled) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference == selected)
            {
                Reads++;
                if (cancelled is not null)
                {
                    cancelled.Cancel();
                    ct.ThrowIfCancellationRequested();
                }
                throw missing ? new ImmutableContentUnavailableException("The exact admitted source manifest is not visible yet.")
                    : new ImmutableContentIntegrityException("The exact admitted source manifest is corrupt.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class EqualWinnerContent(IImmutableContentStore inner, Func<Task> winner) : IImmutableContentStore
    {
        public bool Triggered { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Triggered && schema == EvidenceRedactionNativeSources.Schema)
            {
                Triggered = true;
                await winner();
                throw new ImmutableContentUnavailableException("The losing candidate source write is unavailable after an actual equal winner committed.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
