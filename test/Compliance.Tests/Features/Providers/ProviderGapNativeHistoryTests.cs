using System.Runtime.CompilerServices;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderGapNativeHistoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectUnselectedDuplicateLogicalIdOrPairGivenDefaultOrExecutorHydration(bool duplicatePair,
        bool execute)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var seed = await SeedAsync(host);
        var gap = await host.GapAsync(seed.Request.GapId);
        var malformed = Malformed(host, gap, seed.Admitted, duplicatePair);
        DomainEventSeed.Attach(malformed, gap.Id, gap.Version + 1);
        await host.Events.AppendAsync(gap.Stream, gap.Version, [malformed]);
        await using var scope = host.Services.CreateAsyncScope();
        var entered = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            var selected = new ProviderAssuranceGap(host.Tenant, host.ProviderId, gap.GapId);
            if (execute)
                await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(selected, _ =>
                { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
            else
                await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(selected);
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(entered);
        Assert.Equal(4, (await host.FactsAsync(gap.Stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectUnselectedMalformedPairGivenAlreadyAcceptedPublicRetry()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var seed = await SeedAsync(host);
        var gap = await host.GapAsync(seed.Request.GapId);
        var malformed = Malformed(host, gap, seed.Admitted, duplicatePair: true);
        DomainEventSeed.Attach(malformed, gap.Id, gap.Version + 1);
        await host.Events.AppendAsync(gap.Stream, gap.Version, [malformed]);

        // Act
        var replay = await host.SendAsync(seed.Request);

        // Assert
        Assert.False(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, replay.Error?.Kind);
        Assert.False(replay.Error!.IsTransient);
        Assert.Equal(4, (await host.FactsAsync(gap.Stream)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectSubstitutedUnselectedIntentOrTruncatedPrefixGivenActualHydratedCut(bool truncate)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var seed = await SeedAsync(host);
        var gap = await host.GapAsync(seed.Request.GapId);
        var facts = (await host.FactsAsync(gap.Stream)).ToArray();
        if (truncate)
            facts = facts[..^1];
        else
        {
            var original = (ProviderCoverageGapRiskAcceptanceAdmitted)facts[1];
            var replacement = original with { Intent = ((ProviderCoverageGapRiskAcceptanceAdmitted)facts[2]).Intent };
            replacement.AttachMetadata(original.Metadata);
            facts[1] = replacement;
        }
        await using var scope = host.Services.CreateAsyncScope();
        var reader = new ProviderGapAggregateReader(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            new Records(gap.Stream, facts));

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.HydrateAsync(
            new ProviderAssuranceGap(host.Tenant, host.ProviderId, gap.GapId)));

        // Assert
        if (truncate)
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3, (await host.FactsAsync(gap.Stream)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireSelectedPairAbsenceGivenExistingGapAppend(bool alreadySeen)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var seed = await SeedAsync(host);
        var gap = await host.GapAsync(seed.Request.GapId);
        var request = seed.Request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedGapRevision = gap.Revision,
            RiskId = alreadySeen ? seed.Request.RiskId : Uuid.CreateVersion4()
        };
        var pair = ProviderAssuranceGap.PairEpisode(request.RiskId, request.AcceptanceId);
        var operation = gap.Provenance(request.CommandId, nameof(LinkProviderCoverageGapRiskAcceptance),
            seed.Admitted.Operation.Actor, pair, (ulong)gap.Revision, AuthoredIntentDigest.Compute(request), DateTimeOffset.UtcNow);
        await using var scope = host.Services.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new ProviderAssuranceGap(host.Tenant, host.ProviderId, gap.GapId, request.CommandId,
                selectedPair: alreadySeen ? pair : null), owner => AggregateOutcome.Commit(owner.AdmitLink(operation,
                    gap.Revision, request.RiskId, request.AcceptanceId, seed.Admitted.Intent, seed.Admitted.FirstResponse!)),
            new RequestDispatchContext(RequestActor.System));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(!alreadySeen, result.Error!.IsTransient);
        Assert.Equal(3, (await host.FactsAsync(gap.Stream)).Count);
    }

    static ProviderCoverageGapRiskAcceptanceAdmitted Malformed(ProviderAssuranceHost host, ProviderAssuranceGap gap,
        ProviderCoverageGapRiskAcceptanceAdmitted original, bool duplicatePair)
    {
        var parts = original.Operation.Episode.Split(':');
        var request = new LinkProviderCoverageGapRiskAcceptance(duplicatePair ? Uuid.CreateVersion4() : original.Operation.LogicalId,
            host.Tenant, ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, gap.Revision,
            duplicatePair ? Uuid.Parse(parts[0], CultureInfo.InvariantCulture) : Uuid.CreateVersion4(), Uuid.Parse(parts[1], CultureInfo.InvariantCulture));
        return original with
        {
            Revision = gap.Revision + 1,
            Generation = gap.Revision + 1,
            Operation = original.Operation with
            {
                LogicalId = request.CommandId,
                RootOperationId = request.CommandId,
                Episode = ProviderAssuranceGap.PairEpisode(request.RiskId, request.AcceptanceId),
                OriginalExpectedRevision = (ulong)gap.Revision,
                AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request)
            }
        };
    }

    static async Task<Seed> SeedAsync(ProviderAssuranceHost host)
    {
        var gap = await host.RecordGapAsync();
        LinkProviderCoverageGapRiskAcceptance? first = null;
        for (var index = 0; index < 2; index++)
        {
            var risk = await host.AcceptRiskAsync();
            var request = new LinkProviderCoverageGapRiskAcceptance(Uuid.CreateVersion4(), host.Tenant,
                ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, index + 1, risk.RiskId, risk.AcceptanceId);
            var result = await host.SendAsync(request);
            Assert.True(result.IsSuccess, result.Error?.Message);
            first ??= request;
        }
        var root = await host.RootAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId), first!.CommandId);
        return new Seed(first!, (ProviderCoverageGapRiskAcceptanceAdmitted)root.Event);
    }

    sealed record Seed(LinkProviderCoverageGapRiskAcceptance Request, ProviderCoverageGapRiskAcceptanceAdmitted Admitted);

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
