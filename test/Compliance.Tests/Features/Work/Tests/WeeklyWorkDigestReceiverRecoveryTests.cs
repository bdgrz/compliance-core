using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Processes;
using Bdgrz.Compliance.Features.Work.Services;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Handlers;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WeeklyWorkDigestReceiverRecoveryTests
{
    [Fact]
    public async Task ShouldRecoverOneAdmissionGivenDeliveryAppendCommittedBeforeUnknownResponse()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId, member, digest, source,
            1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        var lost = new CommitThenFailOnceExecutor(fixture.Executor);
        var operations = new WeeklyDigestOperations(new NativeOperationExecutor(fixture.Reader, lost, fixture.Content),
            fixture.Events, fixture.Sources, fixture.Content, new WeeklyWorkDigestRecipientResolver(fixture.Reader, new UnusedAddresses()), fixture.Clock);
        // A recipient resolver with no addresses would suppress a fresh request; use a current eligible recipient via normal handler first.
        // The injected lost response applies to an exact new NoChange receipt, whose existing attempt skips fresh recipients.
        Assert.True((await fixture.DeliveryHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var noChange = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 };

        // Act
        var recovered = await new DeliverWeeklyWorkDigestHandler(operations).HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(noChange, source), CancellationToken.None);
        var replay = await fixture.DeliveryHandler().HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(noChange, source), CancellationToken.None);
        var history = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(3, history.View.Revision);
        Assert.Single(history.View.DeliveryAttempts);
        Assert.Single(history.Facts.OfType<WeeklyWorkDigestDeliveryRequested>(), fact => fact.NoChange && fact.Operation.LogicalId == noChange.CommandId);
    }

    [Theory]
    [InlineData("physical")]
    [InlineData("realm")]
    [InlineData("global")]
    [InlineData("source")]
    [InlineData("actor")]
    [InlineData("intent")]
    [InlineData("no_change")]
    [InlineData("result")]
    public async Task ShouldRefuseContradictoryNativeReceiverGivenExactHydratedHistory(string fault)
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId,
            RbacIds.Member(fixture.TenantId, user), digest, source, 1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        Assert.True((await fixture.DeliveryHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var before = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);
        var fact = Assert.Single(before.Facts.OfType<WeeklyWorkDigestDeliveryRequested>());
        var changed = fault switch
        {
            "physical" => fact with { SourceProof = fact.SourceProof with { PhysicalPosition = fact.SourceProof.PhysicalPosition + 1 } },
            "realm" => fact with { SourceProof = fact.SourceProof with { RealmPosition = fact.SourceProof.RealmPosition + 1 } },
            "global" => fact with { SourceProof = fact.SourceProof with { GlobalPosition = fact.SourceProof.GlobalPosition + 1 } },
            "source" => fact with { SourceProof = fact.SourceProof with { Source = new EventStreamAddress(fixture.TenantId.ToString(), WeeklyWorkDigest.Area, Uuid.CreateVersion4().ToString()) } },
            "actor" => fact with { OriginalActor = fact.OriginalActor with { Subject = "another-process" } },
            "intent" => fact with { Authored = fact.Authored with { ExpectedRevision = 999 } },
            "no_change" => fact with { NoChange = true },
            "result" => fact with { OperationResult = fact.ResponseContent },
            _ => throw new InvalidOperationException()
        };
        changed.AttachMetadata(fact.Metadata);
        var raw = new AlteredEvents(fixture.Events, record => record.Event.Metadata.EventId == fact.Metadata.EventId ? record with { Event = changed } : record);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId, digest));

        // Act
        var failure = await Record.ExceptionAsync(async () => await WeeklyWorkDigestHistory.ReadAsync(raw, fixture.Sources, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.IsNotType<ImmutableContentUnavailableException>(failure);
    }

    sealed class UnusedAddresses : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) => ValueTask.FromResult<EmailAddressView?>(null);
        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<EmailAddressView>([], null));
    }
    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                yield return alter(record);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                yield return alter(record);
        }
    }
}
