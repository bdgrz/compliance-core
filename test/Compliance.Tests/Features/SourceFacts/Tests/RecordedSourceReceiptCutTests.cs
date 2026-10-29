using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

public sealed class RecordedSourceReceiptCutTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly EventStreamAddress Stream = new(Tenant.ToString(), "tenants", Owner.ToString());
    static readonly DateTimeOffset Cut = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExcludePreparedFactGivenCommitReceiptAfterInclusiveCut()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddHours(-2), Cut.AddHours(-1), Cut.AddMilliseconds(-1), Cut.AddMilliseconds(1));

        // Act
        var selected = await fixture.Cut.ReadAsync(Stream);

        // Assert
        Assert.True(selected.Includes(fixture.Facts[0]));
        Assert.False(selected.Includes(fixture.Facts[1]));
        Assert.Equal(Cut.AddMilliseconds(1), selected.RecordedAt(fixture.Facts[1]));
    }

    [Fact]
    public async Task ShouldAcceptCausalPrefixGivenOccurrenceTimestampsDescend()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddMinutes(-1), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));

        // Act
        var selected = await fixture.Cut.ReadAsync(Stream);

        // Assert
        Assert.Equal(2, selected.Included.Count());
        Assert.True(selected.Includes(fixture.Facts[0]));
        Assert.True(selected.Includes(fixture.Facts[1]));
    }

    [Fact]
    public async Task ShouldFailClosedGivenReceiptCutWouldSelectNonPrefix()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(1), Cut.AddMilliseconds(-1));

        // Act
        var failure = await Record.ExceptionAsync(async () => await fixture.Cut.ReadAsync(Stream));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldIncludeAllTiedReceiptsGivenSameBrokerMillisecondAsCut()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddTicks(100), Cut.AddTicks(9000));

        // Act
        var selected = await fixture.Cut.ReadAsync(Stream);

        // Assert
        Assert.Equal(2, selected.Included.Count());
        Assert.All(selected.Records, record => Assert.Equal(Cut, selected.RecordedAt(record.Event)));
    }

    [Fact]
    public async Task ShouldReuseOneCapturedReceiptStreamGivenConcurrentFamilyReads()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));

        // Act
        var reads = await Task.WhenAll(fixture.Cut.ReadAsync(Stream), fixture.Cut.ReadAsync(Stream), fixture.Cut.ReadAsync(Stream));

        // Assert
        Assert.Same(reads[0], reads[1]);
        Assert.Same(reads[0], reads[2]);
        Assert.Equal(2, fixture.Receipts.Reads);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("version")]
    [InlineData("metadata")]
    public async Task ShouldRefuseSelectedSourceGivenTupleOrBytesDiffer(string changed)
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));
        var selected = await fixture.Cut.ReadAsync(Stream);
        var original = (TenantRegistered)fixture.Facts[0];
        var altered = original with { Name = changed == "payload" ? "Altered" : original.Name };
        altered.AttachMetadata(original.Metadata with
        {
            AggregateVersion = changed == "version" ? 9UL : original.Metadata.AggregateVersion,
            OccurredOn = changed == "metadata" ? original.Metadata.OccurredOn.AddSeconds(1) : original.Metadata.OccurredOn
        });

        // Act
        var failure = Record.Exception(() => selected.Includes(altered));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
    }

    [Fact]
    public async Task ShouldRemainPendingGivenExactReceiptAbsent()
    {
        // Arrange
        var source = new InMemoryEventStore();
        var fact = Fact(1, Cut.AddDays(-1));
        await source.AppendAsync(Stream, 0, [fact]);
        var cut = new RecordedSourceReceiptCutFactory(source, new ScriptedNativeEventReceipts()).Create(Tenant, Cut);

        // Act
        var failure = await Record.ExceptionAsync(async () => await cut.ReadAsync(Stream));

        // Assert
        Assert.IsType<ImmutableContentUnavailableException>(failure);
    }

    [Fact]
    public async Task ShouldRejectForeignRealmGivenScopedReceiptSelection()
    {
        // Arrange
        var fixture = await FixtureAsync(Cut.AddDays(-2), Cut.AddDays(-1), Cut.AddMilliseconds(-2), Cut.AddMilliseconds(-1));

        // Act
        var failure = await Record.ExceptionAsync(async () => await fixture.Cut.ReadAsync(new(Uuid.CreateVersion4().ToString(), Stream.Area, Stream.Resource)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(0, fixture.Receipts.Reads);
    }

    [Fact]
    public async Task ShouldAvoidReceiptAuthorityGivenOrdinaryTypeISourceContext()
    {
        // Arrange
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, []);
        var unknown = Fact(1, Cut.AddYears(1));

        // Act
        var included = await context.IncludesAsync(Stream, unknown, CancellationToken.None);

        // Assert
        Assert.True(included);
        Assert.Null(context.ReceiptCut);
    }

    static async Task<(RecordedSourceReceiptCut Cut, ScriptedNativeEventReceipts Receipts, IReadOnlyList<DomainEvent> Facts)> FixtureAsync(
        DateTimeOffset firstOccurred, DateTimeOffset secondOccurred, DateTimeOffset firstCommitted, DateTimeOffset secondCommitted)
    {
        var events = new InMemoryEventStore();
        DomainEvent[] facts = [Fact(1, firstOccurred), Fact(2, secondOccurred)];
        await events.AppendAsync(Stream, 0, facts);
        var receipts = new ScriptedNativeEventReceipts();
        await foreach (var record in events.ReadAsync(Stream, 0, CancellationToken.None))
            receipts.Record(record, record.ResourceOffset == 0 ? firstCommitted : secondCommitted);
        return (new RecordedSourceReceiptCutFactory(events, receipts).Create(Tenant, Cut), receipts, facts);
    }
    static TenantRegistered Fact(ulong version, DateTimeOffset occurred)
    {
        var fact = new TenantRegistered(Tenant, Uuid.CreateVersion4(), "Original", "original");
        fact.AttachMetadata(new(Uuid.CreateVersion4(), Owner, version, occurred));
        return fact;
    }
}
