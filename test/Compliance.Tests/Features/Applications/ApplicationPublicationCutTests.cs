using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

/// <summary>
///     Design trace LOCK-10 for declared applications: one response reads every application at one global frontier,
///     so a batch publication that lands after the cut is invisible to every later page of the same logical read.
/// </summary>
public sealed class ApplicationPublicationCutTests
{
    [Fact]
    public async Task ShouldKeepPublicationCutGivenLaterCommitBetweenListPages()
    {
        // Arrange: two create targets are held and applied, the batch is not committed yet.
        await using var host = new ApplicationImportHost();
        var (streams, serializer) = Scripted();
        var a = Uuid.CreateVersion4();
        var b = Uuid.CreateVersion4();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(a, "record-a"),
            ApplicationImportHost.Create(b, "record-b")]);
        var record = await host.StartProcessAsync(batch.Id);
        await StepAsync(host, record, "claim", nameof(ClaimApplicationImportSource), record.Targets[0]);
        foreach (var target in new[] { a, b }.OrderBy(id => ApplicationImportEffects.TargetKey(id),
                     StreamKeyOrder.Instance).Select(ApplicationImportEffects.TargetKey))
        {
            await StepAsync(host, record, "reserve", nameof(ReserveApplicationImportTarget), target);
            await StepAsync(host, record, "apply", nameof(ApplyApplicationImportEffect), target);
        }

        var batchStream = host.NewBatch(batch.Id).Stream;
        await CopyAsync(host, streams, serializer, host.NewApplication(a).Stream);
        await CopyAsync(host, streams, serializer, host.NewApplication(b).Stream);
        var copied = await CopyAsync(host, streams, serializer, batchStream);
        var publication = new ApplicationPublicationReader(new FitzAuthoritativeSnapshotReader(streams, serializer));
        var first = await publication.BeginAsync();

        // Act: the batch publishes after the cut, between two pages of one logical read.
        var commit = await host.SendAsync((await host.StepAsync(record, "publish",
            nameof(CommitApplicationImportBatch)))!);
        await CopyAsync(host, streams, serializer, batchStream, skip: copied);
        var pageOne = await first.ReadAsync(host.TenantId, a);
        var resumed = await publication.ResumeAsync(first.Cut.GlobalWatermarkExclusive);
        var pageTwo = await resumed.ReadAsync(host.TenantId, b);
        var next = await publication.BeginAsync();
        var freshA = await next.ReadAsync(host.TenantId, a);
        var freshB = await next.ReadAsync(host.TenantId, b);

        // Assert: neither page sees the later publication; the next logical read sees it for every target.
        Assert.True(commit.IsSuccess, commit.Error?.Message);
        Assert.False(pageOne.IsCreated);
        Assert.False(pageTwo.IsCreated);
        Assert.Equal(first.Cut.GlobalWatermarkExclusive, resumed.Cut.GlobalWatermarkExclusive);
        Assert.True(next.Cut.GlobalWatermarkExclusive > first.Cut.GlobalWatermarkExclusive);
        Assert.True(freshA.IsCreated);
        Assert.True(freshB.IsCreated);
        Assert.Equal(1, freshA.Revision);
        Assert.Equal(1, freshB.Revision);
    }

    [Fact]
    public async Task ShouldExcludeFirstAppendGivenEmptyCutThenNextCutIncludesIt()
    {
        // Arrange: nothing exists yet, so the captured frontier is zero.
        var (streams, serializer) = Scripted();
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var publication = new ApplicationPublicationReader(new FitzAuthoritativeSnapshotReader(streams, serializer));
        var empty = await publication.BeginAsync();
        var declared = new DeclaredApplication(tenantId, applicationId);
        Assert.True(declared.Declare("Payroll", "Run payroll", null, Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow).IsSuccess);
        streams.Append(declared.Stream, serializer.Serialize(
            new AggregateScenario<DeclaredApplication>(declared).PendingEvents.Single()).ToArray());

        // Act
        var original = await empty.ReadAsync(tenantId, applicationId);
        var later = await (await publication.BeginAsync()).ReadAsync(tenantId, applicationId);

        // Assert
        Assert.Equal(0UL, empty.Cut.GlobalWatermarkExclusive);
        Assert.False(original.IsCreated);
        Assert.True(later.IsCreated);
    }

    [Fact]
    public async Task ShouldFailClosedGivenWithheldApplicationRecord()
    {
        // Arrange
        var (streams, serializer) = Scripted();
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        streams.Append(new EventStreamAddress(tenantId.ToString(), DeclaredApplication.Area, applicationId.ToString()),
            Array.Empty<byte>(), filtered: true);
        var cut = await new ApplicationPublicationReader(new FitzAuthoritativeSnapshotReader(streams, serializer))
            .BeginAsync();

        // Act
        var failure = await Record.ExceptionAsync(async () => await cut.ReadAsync(tenantId, applicationId));

        // Assert: a withheld page is a failed read, never an absent application.
        Assert.IsType<SourceFactVerificationException>(failure);
    }

    [Fact]
    public void ShouldCarryTheCutWatermarkGivenListCursor()
    {
        // Arrange
        var wrapped = ApplicationPublicationCursor.Wrap(42, "application_import_missing_v1:a:b:1:2:3");

        // Act
        var unwrapped = ApplicationPublicationCursor.Unwrap(wrapped);
        var first = ApplicationPublicationCursor.Unwrap(null);
        var invalid = ApplicationPublicationCursor.Unwrap("publication_cut_v1:x:inner");

        // Assert
        Assert.Equal(42UL, unwrapped.Value.Watermark);
        Assert.Equal("application_import_missing_v1:a:b:1:2:3", unwrapped.Value.Inner);
        Assert.Null(first.Value.Watermark);
        Assert.Null(first.Value.Inner);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
        Assert.Null(ApplicationPublicationCursor.Wrap(42, null));
    }

    static (ScriptedStreamClient Streams, IDomainEventSerializer Serializer) Scripted()
    {
        var provider = EventPersistenceFrameTests.Compose();
        return (new ScriptedStreamClient(), provider.GetRequiredService<IDomainEventSerializer>());
    }

    static async Task<int> CopyAsync(ApplicationImportHost host, ScriptedStreamClient streams,
        IDomainEventSerializer serializer, EventStreamAddress stream, int skip = 0)
    {
        var index = 0;
        await foreach (var record in host.Events.ReadAsync(stream, 0))
        {
            if (index++ < skip)
                continue;
            streams.Append(stream, serializer.Serialize(record.Event).ToArray());
        }

        return index;
    }

    static async Task StepAsync(ApplicationImportHost host, ProcessRecord record, string stage, string receiver,
        string target)
    {
        var result = await host.SendAsync((await host.StepAsync(record, stage, receiver, target))!);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
}
