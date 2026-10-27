using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class AuditPackageNativeHistoryTests
{
    [Theory]
    [InlineData("missing", true)]
    [InlineData("foreign_stream", false)]
    [InlineData("physical_gap", false)]
    [InlineData("historical_payload", false)]
    public async Task ShouldRefuseMissingOrContradictoryPackageHistoryGivenExactHydratedCut(string fault, bool transient)
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var package = await fixture.SeedBuiltAndValidatedPackageAsync();
        var rules = await fixture.ReadPackageAsync(package);
        var previous = rules.Current!.Versions[0];
        Assert.Null(rules.Amend(rules.Revision, "Exact successor", Uuid.CreateVersion4(),
            previous.SourceSnapshotSha256, previous.SelectedContent, previous.CreatedByMemberId,
            previous.CreatedByDisplay, previous.CreatedAt.AddMinutes(1)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.ContentStore,
            rules.Current!);
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, package));
        var raw = new AlteredEvents(fixture.PeriodEvents, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with
            {
                Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(),
                record.Stream.Area, record.Stream.Resource)
            },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            "historical_payload" when record.Event is AuditPackageCreated created =>
                record with { Event = Changed(created) },
            _ => record,
        });

        // Act
        var failure = await Record.ExceptionAsync(async () => await AuditPackageManifestHistory.ReadAsync(raw, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(transient, failure is ImmutableContentUnavailableException);
    }

    [Fact]
    public async Task ShouldComposeCapturedHistoricalVersionGivenLaterManifestNativeFacts()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var package = await fixture.SeedBuiltAndValidatedPackageAsync();
        var captured = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, package,
            selectedVersion: 1));
        var history = await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, captured);
        var original = history.VersionContent(1);
        var rules = await fixture.ReadPackageAsync(package);
        var previous = rules.Current!.Versions[0];
        Assert.Null(rules.Amend(rules.Revision, "Exact successor", Uuid.CreateVersion4(),
            previous.SourceSnapshotSha256, previous.SelectedContent, previous.CreatedByMemberId,
            previous.CreatedByDisplay, previous.CreatedAt.AddMinutes(1)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.ContentStore,
            rules.Current!);

        // Act
        var prior = await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, captured);
        var current = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, package,
            selectedVersion: 1));
        var all = await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, current);

        // Assert
        Assert.Equal(4, prior.Facts.Count);
        Assert.Equal(1, captured.CurrentVersion);
        Assert.Equal(2, current.CurrentVersion);
        Assert.Equal(original, current.SelectedContent);
        Assert.Equal(original, all.VersionContent(1));
        Assert.NotEqual(current.CurrentContent, current.SelectedContent);
    }

    [Fact]
    public async Task ShouldRequireExplicitSelectedFinalizedAuthorityGivenDirectPackageAppend()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var package = await fixture.SeedDraftPackageAsync();
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, package));
        var logical = Uuid.CreateVersion4();
        var prior = Assert.IsType<AuditPackageCreated>(owner.Latest);
        var operation = prior.Operation with
        {
            LogicalId = logical,
            RootOperationId = logical,
            CommandKind = nameof(AmendAuditPackage),
            OriginalExpectedRevision = (ulong)owner.Revision,
            Episode = "2"
        };

        // Act
        var failure = owner.Amend(fixture.ProgramId, owner.Revision, operation, prior.Change,
            prior.FirstResponse!, prior.OperationResult!);

        // Assert
        Assert.False(failure.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, failure.Error?.Kind);
        Assert.True(failure.Error?.IsTransient);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public async Task ShouldRefusePreparedBuildProofGivenLaterNativeManifestCut()
    {
        // Arrange
        await using var fixture = new RecordAuditPackageBuildResultHandlerTests.Fixture();
        var first = await fixture.StartBuildAsync("first", storeSources: false);
        var selected = await fixture.OwnerAsync();
        var history = await AuditPackageManifestHistory.ReadAsync(fixture.Inner.PeriodEvents, selected);
        var proof = AuditPackagePreparedProof.ForBuild(history, first.Source, first.ManifestVersion, first.OperationId);
        Assert.True(proof.IsSuccess);
        var admission = Assert.IsType<AuditPackageBuildRequested>(history.FactById(first.Source.EventId));
        var intent = await fixture.Inner.ContentStore.ReadJsonAsync<PackageBuildIntent>(fixture.Inner.TenantId.ToString(),
            admission.Change);
        await fixture.StartBuildAsync("second", storeSources: false);
        var logical = Uuid.CreateVersion4();
        var current = await fixture.Inner.Reader.HydrateAsync(new AuditPackageManifest(fixture.Inner.TenantId,
            fixture.PackageId, logical, nameof(RecordAuditPackageBuildResult), first.ManifestVersion));
        var operation = SourceUseReceiverSupport.Child(logical, nameof(RecordAuditPackageBuildResult), first.Source.EventId,
            intent.Header, current.Stream, DateTimeOffset.UtcNow);
        var result = await PackageContent.FreezeUnitResultAsync(fixture.Inner.ContentStore, fixture.Inner.TenantId,
            CancellationToken.None);

        // Act
        var rejected = current.RecordBuilt(fixture.Inner.ProgramId, current.Revision, first.ManifestVersion, operation,
            first.Source, admission.Change, result, proof.Value!);

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error?.Kind);
        Assert.True(rejected.Error?.IsTransient);
        Assert.Empty((await fixture.HistoryAsync()).Facts.OfType<AuditPackageBuilt>());
    }

    [Fact]
    public async Task ShouldRejectAnotherBuildRootGivenExactPublishedNativeAdmission()
    {
        // Arrange
        await using var fixture = new RecordAuditPackageBuildResultHandlerTests.Fixture();
        var source = await fixture.StartBuildAsync(storeSources: false);
        var history = await fixture.HistoryAsync();

        // Act
        var proof = AuditPackagePreparedProof.ForBuild(history, source.Source, source.ManifestVersion, Uuid.CreateVersion4());

        // Assert
        Assert.False(proof.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, proof.Error?.Kind);
        Assert.False(proof.Error?.IsTransient);
    }

    static AuditPackageCreated Changed(AuditPackageCreated fact)
    {
        var changed = fact with { Change = fact.OperationResult! };
        changed.AttachMetadata(fact.Metadata);
        return changed;
    }

    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
    }
}
