using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIIPeriodCloseHistoryReceiptTests
{
    static readonly DateTimeOffset Cut = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireCompleteLegacyIncidentPrefixGivenControlledBrokerReceiptCut(bool nonPrefix)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var incident = new ComplianceIncident(tenant, program, id);
        var body = new ComplianceIncidentContent(null, null, null, null, "Observed credential exposure",
            "The credential was revoked and scope reviewed.", "security", "medium", Cut.AddDays(-2), Cut.AddDays(-1), member,
            false, false, [Uuid.CreateVersion4()], "internal", [Uuid.CreateVersion4()]);
        Assert.Null(incident.Record(body, member, "Recorder", Cut.AddDays(-1)));
        var initial = incident.Current!;
        Assert.Null(incident.Revise(1, body with { Description = "Independent revised description." }, "Clarify observed scope.",
            member, "Recorder", Cut.AddHours(-1)));
        var revised = incident.Current!;
        var provenance = IncidentFacts.Provenance(incident, nameof(ReviseComplianceIncident), Cut.AddHours(-1));
        DomainEvent[] facts = [
            DomainEventSeed.Attach(new ComplianceIncidentRecorded(tenant, id, 1, member, initial, Cut.AddDays(-1)), id, 1),
            DomainEventSeed.Attach(new ComplianceIncidentRevised(tenant, id, 2, member, revised, Cut.AddHours(-1),
                provenance.LogicalId, provenance, IncidentFacts.Response()), id, 2),
        ];
        var events = new InMemoryEventStore();
        await events.AppendAsync(incident.Stream, 0, facts);
        var receipts = new ScriptedNativeEventReceipts();
        await foreach (var record in events.ReadAsync(incident.Stream, 0, CancellationToken.None))
            receipts.Record(record, (record.ResourceOffset == 0) == nonPrefix ? Cut.AddMilliseconds(1) : Cut.AddMilliseconds(-1));
        var reader = new IncidentReader(incident);
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var history = new TypeIIPeriodCloseHistory(reader, events, new RestrictedComplianceRecordVisibility(new NeverNeededPermissions()),
            new SignificantChangeReader(reader, events, content, receipts), content, receipts);
        var period = new TypeIIPeriodView(tenant, program, Uuid.CreateVersion4(), "observing", 1, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, new string('a', 64), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            Uuid.CreateVersion4(), new string('b', 64), [], [], [], [], null, [], [], member, "Recorder", Cut);
        IReadOnlyList<TypeIIPeriodCloseNativeSource>? captured = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => captured = (await history.ReadAsync(period,
            new ClaimsPrincipal(new ClaimsIdentity()), Cut, CancellationToken.None)).Records);

        // Assert
        if (nonPrefix)
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        else
        {
            Assert.Null(failure);
            var source = Assert.Single(captured!);
            Assert.Equal(facts[0].Metadata.EventId, source.Fact.EventId);
            Assert.Equal(Cut.AddMilliseconds(-1), source.RecordedAt);
        }
    }

    sealed class IncidentReader(ComplianceIncident value) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate =>
            ValueTask.FromResult(aggregate is ComplianceIncident && aggregate.Id == value.Id ? (T)(Aggregate)value : aggregate);
    }
    sealed class NeverNeededPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            throw new InvalidOperationException("Internal source history does not require a restricted permission lookup.");
    }
}
