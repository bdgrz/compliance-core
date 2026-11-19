using System.Globalization;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class PlatformCriteriaSeedBootstrapTests
{
    [Fact]
    public async Task ShouldStopBeforeChildrenGivenConflictingSeedRelease()
    {
        // Arrange
        var writer = new RecordingSeedWriter { BindStatus = CriteriaSeedWriteStatus.Conflict };
        var bootstrap = new PlatformCriteriaSeedBootstrap(writer);

        // Act
        var result = await bootstrap.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Conflict, result.Status);
        Assert.Empty(writer.EntryIds);
        Assert.Empty(writer.DisclosureIds);
        Assert.False(writer.MetadataRecorded);
        Assert.False(writer.PublishRequested);
    }

    [Fact]
    public async Task ShouldLeavePublicationUnrequestedGivenPendingEntry()
    {
        // Arrange
        var writer = new RecordingSeedWriter { PendingIdentifier = "CC1.2" };
        var bootstrap = new PlatformCriteriaSeedBootstrap(writer);

        // Act
        var result = await bootstrap.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal("entry:CC1.2", result.Step);
        Assert.Contains("CC1.1", writer.EntryIds);
        Assert.Contains("CC1.3", writer.EntryIds);
        Assert.Equal(67, writer.EntryIds.Count);
        Assert.False(writer.PublishRequested);
    }

    [Fact]
    public async Task ShouldStopAndNotPublishGivenConflictingEntry()
    {
        // Arrange
        var writer = new RecordingSeedWriter { ConflictIdentifier = "CC1.2" };
        var bootstrap = new PlatformCriteriaSeedBootstrap(writer);

        // Act
        var result = await bootstrap.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Conflict, result.Status);
        Assert.Equal("entry:CC1.2", result.Step);
        Assert.DoesNotContain("CC1.3", writer.EntryIds);
        Assert.False(writer.PublishRequested);
    }

    [Fact]
    public async Task ShouldLeaveCoveragePendingGivenPublicationWithoutCoverageProof()
    {
        // Arrange
        var writer = new RecordingSeedWriter { CoverageStatus = CriteriaSeedWriteStatus.Pending };
        var bootstrap = new PlatformCriteriaSeedBootstrap(writer);

        // Act
        var result = await bootstrap.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Pending, result.Status);
        Assert.Equal("coverage", result.Step);
        Assert.True(writer.PublishRequested);
        Assert.Equal(67, writer.EntryIds.Count);
        Assert.Equal(6, writer.DisclosureIds.Count);
        Assert.Contains("privacy:privacy_lifecycle_unsupported", writer.DisclosureIds);
        Assert.Equal("CC1.1", writer.WitnessIdentifier);
    }

    [Fact]
    public async Task ShouldReuseTheSameIdentitiesGivenRepeatedBootstrap()
    {
        // Arrange
        var first = new RecordingSeedWriter();
        var second = new RecordingSeedWriter();

        // Act
        var firstResult = await new PlatformCriteriaSeedBootstrap(first).RunAsync(CancellationToken.None);
        var secondResult = await new PlatformCriteriaSeedBootstrap(second).RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, firstResult.Status);
        Assert.Equal(CriteriaSeedWriteStatus.Confirmed, secondResult.Status);
        Assert.Equal(first.Header, second.Header);
        Assert.Equal(first.OperationIds, second.OperationIds);
        Assert.Equal("bdgrz-tsc-2017-2022-v1", first.Header!.SeedVersion);
        Assert.True(first.Header.NumberedCriteriaComplete);
        Assert.Contains("partially mapped", first.Header.CoverageNote, StringComparison.Ordinal);
    }

    sealed class RecordingSeedWriter : ICriteriaSeedWriter
    {
        static readonly Uuid BindingEventId = Uuid.Parse("11223344-1111-4111-8111-000000000001",
            CultureInfo.InvariantCulture);

        public CriteriaSeedWriteStatus BindStatus { get; init; } = CriteriaSeedWriteStatus.Confirmed;
        public CriteriaSeedWriteStatus CoverageStatus { get; init; } = CriteriaSeedWriteStatus.Confirmed;
        public string? PendingIdentifier { get; init; }
        public string? ConflictIdentifier { get; init; }
        public CriteriaSeedHeader? Header { get; private set; }
        public List<string> EntryIds { get; } = [];
        public List<string> DisclosureIds { get; } = [];
        public List<Uuid> OperationIds { get; } = [];
        public bool PublishRequested { get; private set; }
        public bool MetadataRecorded { get; private set; }
        public string? WitnessIdentifier { get; private set; }

        public ValueTask<CriteriaSeedWriteResult> BindEditionAsync(CriteriaSeedHeader header,
            Uuid operationId, CancellationToken ct)
        {
            Header = header;
            OperationIds.Add(operationId);
            return ValueTask.FromResult(new CriteriaSeedWriteResult(BindStatus,
                BindStatus == CriteriaSeedWriteStatus.Confirmed ? BindingEventId : Uuid.Empty));
        }

        public ValueTask<CriteriaSeedWriteResult> EnsureMetadataAsync(CriteriaSeedHeader header,
            Uuid bindingEventId, Uuid operationId, CancellationToken ct)
        {
            Assert.Equal(BindingEventId, bindingEventId);
            OperationIds.Add(operationId);
            MetadataRecorded = true;
            return ValueTask.FromResult(new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Confirmed, Uuid.Empty));
        }

        public ValueTask<CriteriaSeedWriteResult> EnsureDisclosureAsync(CriteriaSeedHeader header,
            Uuid bindingEventId, Uuid operationId, CriteriaSupportGap gap, CancellationToken ct)
        {
            Assert.Equal(BindingEventId, bindingEventId);
            OperationIds.Add(operationId);
            DisclosureIds.Add(gap.Category + ":" + gap.Code);
            return ValueTask.FromResult(new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Confirmed, Uuid.Empty));
        }

        public ValueTask<CriteriaSeedWriteResult> EnsureEntryAsync(CriteriaSeedHeader header,
            Uuid bindingEventId, Uuid operationId, Criterion entry, CancellationToken ct)
        {
            Assert.Equal(BindingEventId, bindingEventId);
            OperationIds.Add(operationId);
            EntryIds.Add(entry.Identifier);
            var status = entry.Identifier == ConflictIdentifier ? CriteriaSeedWriteStatus.Conflict :
                entry.Identifier == PendingIdentifier ? CriteriaSeedWriteStatus.Pending : CriteriaSeedWriteStatus.Confirmed;
            return ValueTask.FromResult(new CriteriaSeedWriteResult(status, Uuid.Empty));
        }

        public ValueTask<CriteriaSeedWriteResult> PublishAsync(CriteriaSeedHeader header,
            Uuid bindingEventId, Uuid operationId, string witnessIdentifier, CancellationToken ct)
        {
            Assert.Equal(BindingEventId, bindingEventId);
            OperationIds.Add(operationId);
            PublishRequested = true;
            WitnessIdentifier = witnessIdentifier;
            return ValueTask.FromResult(new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Confirmed, Uuid.Empty));
        }

        public ValueTask<CriteriaSeedWriteResult> ConfirmCoverageAsync(CriteriaSeedHeader header,
            Uuid bindingEventId, CancellationToken ct)
        {
            Assert.Equal(BindingEventId, bindingEventId);
            return ValueTask.FromResult(new CriteriaSeedWriteResult(CoverageStatus, Uuid.Empty));
        }
    }
}
