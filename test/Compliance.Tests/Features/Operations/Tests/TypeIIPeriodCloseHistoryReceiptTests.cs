using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Handlers;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

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
