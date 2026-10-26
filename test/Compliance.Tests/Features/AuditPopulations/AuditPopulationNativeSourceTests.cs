using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed partial class DefineAuditPopulationHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyActualAcceptedSourceFailureWithoutAppendGivenAuditPopulationNativeProof(bool missing)
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = await SeedNativeRevisionAsync(fixture);
        var complete = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        var fact = Assert.IsType<AuditPopulationDefinitionRevisedV2>(complete.Facts[^1]);
        var fault = new PopulationReadFault(fixture.PeriodContent, fact.SourceProofs, missing, null);
        var handler = new ReviseAuditPopulationDefinitionHandler(fixture.MutationsFor(fault));
        var context = PopulationContext(fixture, request, RequestMetadata.Create());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var after = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.True(fault.Reads > 0);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(missing, result.Error.IsTransient);
        Assert.Equal(2, after.View!.Revision);
        Assert.Equal(complete.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyActualFrozenCloseBodyFailureWithoutAppendGivenAuditPopulationNativeSource(bool missing)
    {
        // Arrange
        await using var fixture = new Fixture();
        await SeedPublicPopulationAsync(fixture);
        ObservationPeriodCloseFrozen? close = null;
        await foreach (var record in fixture.PeriodEvents.ReadAsync(new EventStreamAddress(fixture.TenantId.ToString(),
            TypeIIPeriod.Area, fixture.PeriodId.ToString()), 0))
            if (record.Event is ObservationPeriodCloseFrozen actual)
                close = actual;
        Assert.NotNull(close);
        var fault = new PopulationReadFault(fixture.PeriodContent, close!.CurrentContent, missing, null);

        // Act
        var result = await new ReviseAuditPopulationDefinitionHandler(fixture.MutationsFor(fault)).HandleAsync(
            PopulationContext(fixture, Revision(fixture), RequestMetadata.Create()), CancellationToken.None);
        var after = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.True(fault.Reads > 0);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(missing, result.Error.IsTransient);
        Assert.Equal(1, after.View!.Revision);
        Assert.Single(after.Facts);
    }

    [Fact]
    public async Task ShouldPropagateRequestedCancellationWithoutAppendGivenAuditPopulationNativeProofReadCancels()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = await SeedNativeRevisionAsync(fixture);
        var complete = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        var fact = Assert.IsType<AuditPopulationDefinitionRevisedV2>(complete.Facts[^1]);
        using var cancellation = new CancellationTokenSource();
        var fault = new PopulationReadFault(fixture.PeriodContent, fact.SourceProofs, true, cancellation);
        var handler = new ReviseAuditPopulationDefinitionHandler(fixture.MutationsFor(fault));

        // Act
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handler.HandleAsync(PopulationContext(fixture, request, RequestMetadata.Create()), cancellation.Token));

        // Assert
        Assert.True(fault.Reads > 0);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(2, (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId)).View!.Revision);
    }

    [Fact]
    public async Task ShouldReplayActualEqualWinnerWithoutAppendGivenAuditPopulationNativeSourceCandidateWriteFails()
    {
        // Arrange
        await using var fixture = new Fixture();
        await SeedPublicPopulationAsync(fixture);
        var request = Revision(fixture);
        var metadata = RequestMetadata.Create();
        var fault = new PopulationEqualWinner(fixture.PeriodContent, async () =>
        {
            var winner = await new ReviseAuditPopulationDefinitionHandler(fixture.Mutations)
                .HandleAsync(PopulationContext(fixture, request, metadata), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
        });

        // Act
        var result = await new ReviseAuditPopulationDefinitionHandler(fixture.MutationsFor(fault))
            .HandleAsync(PopulationContext(fixture, request, metadata), CancellationToken.None);
        var after = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.True(fault.Triggered);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, after.View!.Revision);
        Assert.Single(after.Facts.OfType<AuditPopulationDefinitionRevisedV2>());
    }

    [Fact]
    public async Task ShouldBindActualScopedPredecessorAndFrozenCloseLocatorsGivenAuditPopulationNativeDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await SeedNativeRevisionAsync(fixture);
        var complete = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        var fact = Assert.IsType<AuditPopulationDefinitionRevisedV2>(complete.Facts[^1]);
        var sources = await NativeContent.ReadAsync<AuditPopulationDecisionSource>(fixture.PeriodContent, fixture.TenantId, fact.SourceProofs);
        var resolver = new EventStoreSourceFactResolver(fixture.PeriodEvents);

        // Act
        var predecessor = await resolver.ResolveAsync(sources.Predecessor.Source, sources.Predecessor.ResourcePosition);
        var close = await resolver.ResolveAsync(sources.CloseFact.Source, sources.CloseFact.ResourcePosition);

        // Assert
        Assert.Equal(complete.Owner.Stream, predecessor.Locator.Source);
        Assert.Equal(complete.Facts[0].Metadata.EventId, predecessor.Locator.EventId);
        Assert.Equal(sources.Predecessor, predecessor.Locator);
        Assert.Equal(sources.CloseFact, close.Locator);
        Assert.Equal(fixture.PeriodId.ToString(), close.Locator.Source.Resource);
        Assert.Equal("type-ii-periods", close.Locator.Source.Area);
        Assert.NotEqual(close.Locator.ResourcePosition, close.Locator.RealmPosition);
        Assert.NotEqual(close.Locator.ResourcePosition, close.Locator.GlobalPosition);
        Assert.Equal(1UL, sources.PhysicalPosition);
        Assert.Equal(1, sources.Revision);
        Assert.Equal(complete.View!.CloseSnapshot.CloseSnapshotId, sources.CloseSnapshotId);
        Assert.Equal(complete.View.CloseSnapshot.ContentSha256, sources.CloseSnapshotSha256);
    }

    [Fact]
    public async Task ShouldRefuseCapturedTargetAfterRealSuccessorGivenAuditPopulationNativeResponsePreparation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await SeedPublicPopulationAsync(fixture);
        var request = Revision(fixture);
        var fault = new PopulationSuccessorContent(fixture.PeriodContent, async () =>
        {
            var winner = await new ReviseAuditPopulationDefinitionHandler(fixture.Mutations).HandleAsync(
                PopulationContext(fixture, request with { CommandId = Uuid.CreateVersion4(), ChangeReason = "Actual independent successor" },
                    RequestMetadata.Create()), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
        });

        // Act
        var result = await new ReviseAuditPopulationDefinitionHandler(fixture.MutationsFor(fault)).HandleAsync(
            PopulationContext(fixture, request, RequestMetadata.Create()), CancellationToken.None);
        var retry = await new ReviseAuditPopulationDefinitionHandler(fixture.Mutations).HandleAsync(
            PopulationContext(fixture, request, RequestMetadata.Create()), CancellationToken.None);
        var after = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);

        // Assert
        Assert.True(fault.Triggered);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.False(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        Assert.False(retry.Error.IsTransient);
        Assert.Equal(2, after.View!.Revision);
        Assert.Equal("Actual independent successor", after.View.CurrentDefinition!.ChangeReason);
        Assert.Single(after.Facts.OfType<AuditPopulationDefinitionRevisedV2>());
    }

    static async Task SeedPublicPopulationAsync(Fixture fixture)
    {
        var close = await fixture.SeedClosedPeriodAsync();
        var result = await fixture.Handler().HandleAsync(fixture.Context(new DefineAuditPopulation(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId, fixture.PeriodId, close, fixture.Definition())), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
    static ReviseAuditPopulationDefinition Revision(Fixture fixture) => new(Uuid.CreateVersion4(), fixture.TenantId,
        fixture.ProgramId, fixture.PopulationId, 1, "Approved production changes", "Include approved changes.",
        "Exclude canceled changes.", "Clarify the complete declared source boundary.");
    static async Task<ReviseAuditPopulationDefinition> SeedNativeRevisionAsync(Fixture fixture)
    {
        await SeedPublicPopulationAsync(fixture);
        var request = Revision(fixture);
        var result = await new ReviseAuditPopulationDefinitionHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, request, RequestMetadata.Create()), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return request;
    }

    sealed class PopulationReadFault(IImmutableContentStore inner, ImmutableContentRef selected, bool missing,
        CancellationTokenSource? cancellation) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference == selected)
            {
                Reads++;
                if (cancellation is not null)
                { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); }
                throw missing ? new ImmutableContentUnavailableException("The exact retained source is not visible yet.") :
                    new ImmutableContentIntegrityException("The exact retained source is corrupt.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
    sealed class PopulationSuccessorContent(IImmutableContentStore inner, Func<Task> successor) : IImmutableContentStore
    {
        public bool Triggered { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Triggered && schema == AuditPopulationContent.StateSchema)
            {
                Triggered = true;
                await successor();
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
    sealed class PopulationEqualWinner(IImmutableContentStore inner, Func<Task> winner) : IImmutableContentStore
    {
        public bool Triggered { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Triggered && schema == AuditPopulationDecisionSource.Schema)
            {
                Triggered = true;
                await winner();
                throw new ImmutableContentUnavailableException("The losing source candidate failed after an actual equal winner committed.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
