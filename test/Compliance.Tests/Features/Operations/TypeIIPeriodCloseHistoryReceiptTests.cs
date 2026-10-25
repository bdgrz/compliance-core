using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Features.Incidents;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

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
        await using var fixture = new IncidentNativeFixture();
        fixture.Clock.Now = Cut.AddDays(-1);
        var body = fixture.Body() with { HandlingClass = "internal" };
        await fixture.SeedAsync(body);
        fixture.Clock.Now = Cut.AddHours(-1);
        Assert.True((await new ReviseComplianceIncidentHandler(fixture.Operations).HandleAsync(fixture.Context(
            new ReviseComplianceIncident(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Incident, 1,
                body with { Description = "Independent revised description." }, "Clarify observed scope.")), CancellationToken.None)).IsSuccess);
        var facts = await fixture.FactsAsync();
        var receipts = new ScriptedNativeEventReceipts();
        var stream = new EventStreamAddress(fixture.Tenant.ToString(), ComplianceIncident.Area, fixture.Incident.ToString());
        await foreach (var record in fixture.Events.ReadAsync(stream, 0, CancellationToken.None))
            receipts.Record(record, (record.ResourceOffset == 0) == nonPrefix ? Cut.AddMilliseconds(1) : Cut.AddMilliseconds(-1));
        var incidents = new ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Content, receipts);
        var history = new TypeIIPeriodCloseHistory(fixture.Events, new RestrictedComplianceRecordVisibility(new NeverNeededPermissions()),
            new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Content, receipts), fixture.Content, incidents, receipts);
        var period = new TypeIIPeriodView(fixture.Tenant, fixture.Program, Uuid.CreateVersion4(), "observing", 1, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, new string('a', 64), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            Uuid.CreateVersion4(), new string('b', 64), [], [], [], [], null, [], [], RbacIds.Member(fixture.Tenant, fixture.User), "Recorder", Cut);
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
            Assert.NotEmpty(source.ContentBlobs!);
        }
    }

    sealed class NeverNeededPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            throw new InvalidOperationException("Internal source history does not require a restricted permission lookup.");
    }
}
