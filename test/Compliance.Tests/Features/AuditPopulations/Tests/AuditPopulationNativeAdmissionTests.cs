using System.Security.Claims;
using Cntryl.Portia;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.Aggregates.AuditPopulation;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Handlers;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;

public sealed partial class DefineAuditPopulationHandlerTests
{
    [Theory]
    [InlineData("revise")]
    [InlineData("record")]
    [InlineData("classify")]
    [InlineData("reconcile")]
    [InlineData("freeze")]
    [InlineData("amend")]
    public async Task ShouldReplayAcceptedOriginalUnitWithoutAppendGivenAuditPopulationNativeBoundary(string kind)
    {
        // Arrange: reuse the existing actual period writer/content reader and public definition handler.
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(new DefineAuditPopulation(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId, fixture.PeriodId, close, fixture.Definition())), CancellationToken.None)).IsSuccess);
        var row = PopulationRow(fixture);
        var snapshot = PopulationSource(fixture);
        if (kind is "classify" or "reconcile" or "freeze" or "amend")
            Assert.True((await ExecutePopulationAsync(fixture, new RecordAuditPopulationFacts(Uuid.CreateVersion4(), fixture.TenantId,
                fixture.ProgramId, fixture.PopulationId, 1, snapshot, [row]), RequestMetadata.Create())).IsSuccess);
        if (kind is "freeze" or "amend")
            Assert.True((await ExecutePopulationAsync(fixture, new ReconcileAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId,
                fixture.ProgramId, fixture.PopulationId, 2, "All declared rows were reconciled."), RequestMetadata.Create())).IsSuccess);
        if (kind == "amend")
            Assert.True((await ExecutePopulationAsync(fixture, new FreezeAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId,
                fixture.ProgramId, fixture.PopulationId, 3, "The exact reconciled population was reviewed."), RequestMetadata.Create())).IsSuccess);
        IRequest request = kind switch
        {
            "revise" => new ReviseAuditPopulationDefinition(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
                1, "Approved production changes", "Include approved changes.", "Exclude canceled changes.", "Clarify source boundary."),
            "record" => new RecordAuditPopulationFacts(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PopulationId, 1, snapshot, [row]),
            "classify" => new ClassifyAuditPopulationRow(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
                row.RowId, "excluded", "Outside the selected source window."),
            "reconcile" => new ReconcileAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
                2, "All declared rows were reconciled."),
            "freeze" => new FreezeAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PopulationId,
                3, "The exact reconciled population was reviewed."),
            "amend" => new AmendAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PopulationId, 4,
                "Correct the declared row classification.", PopulationSource(fixture),
                [row with { Classification = "excluded", ClassificationRationale = "Outside the selected window." }]),
            _ => throw new InvalidOperationException()
        };
        var metadata = RequestMetadata.Create();
        var first = await ExecutePopulationAsync(fixture, request, metadata);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId));
        fixture.AdvanceClock();

        // Act
        var retry = await ExecutePopulationAsync(fixture, request, metadata);
        var after = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(close, after.Header!.CloseSnapshotId);
    }

    [Fact]
    public async Task ShouldKeepCompleteDefinitionsOutsideCommandOwnerGivenAuditPopulationNativeBoundary()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedClosedPeriodAsync();
        Assert.True((await fixture.Handler().HandleAsync(fixture.Context(new DefineAuditPopulation(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId, fixture.PeriodId, close, fixture.Definition())), CancellationToken.None)).IsSuccess);
        for (var revision = 1; revision < 24; revision++)
        {
            fixture.AdvanceClock();
            var revised = await ExecutePopulationAsync(fixture, new ReviseAuditPopulationDefinition(Uuid.CreateVersion4(), fixture.TenantId,
                fixture.ProgramId, fixture.PopulationId, revision, "Approved production changes", "Include approved changes.",
                "Exclude canceled changes.", $"Revision reason {revision}"), RequestMetadata.Create());
            Assert.True(revised.IsSuccess, revised.Error?.Message);
        }

        // Act
        var owner = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.PopulationId));

        // Assert
        Assert.Equal(24, owner.Revision);
        var complete = await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PopulationId);
        Assert.Equal(24, complete.View!.DefinitionVersions.Count);
        Assert.Equal(24, owner.Header!.DefinitionVersion);
        Assert.All(typeof(AuditPopulationAggregate).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly), field => Assert.False(field.GetValue(owner) is System.Collections.IEnumerable retained and not string &&
                retained.Cast<object>().Skip(2).Any(), $"The compact command owner retains lifetime history in {field.Name}."));
    }

    static AuditPopulationSourceSnapshotContent PopulationSource(Fixture fixture) =>
        new(Uuid.CreateVersion4(), 1, "manual-approved-change-register", new string('e', 64), 1, true,
            "Every declared manual source row is accounted for.", fixture.Clock.GetUtcNow());
    static AuditPopulationRowContent PopulationRow(Fixture fixture) => new(Uuid.CreateVersion4(), "change-17",
        "manual-approved-change-register", "included", "The declared change falls in the selected period.",
        new Dictionary<string, string> { ["control_id"] = fixture.ControlId.ToString(), ["change_reference"] = "change-17" }, Array.Empty<AuditPopulationRecordReference>(), Array.Empty<Uuid>());

    static ValueTask<Result> ExecutePopulationAsync(Fixture fixture, IRequest request, RequestMetadata metadata) => request switch
    {
        ReviseAuditPopulationDefinition value => new ReviseAuditPopulationDefinitionHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, value, metadata), CancellationToken.None),
        RecordAuditPopulationFacts value => new RecordAuditPopulationFactsHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, value, metadata), CancellationToken.None),
        ClassifyAuditPopulationRow value => new ClassifyAuditPopulationRowHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, value, metadata), CancellationToken.None),
        ReconcileAuditPopulation value => new ReconcileAuditPopulationHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, value, metadata), CancellationToken.None),
        FreezeAuditPopulation value => new FreezeAuditPopulationHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, value, metadata), CancellationToken.None),
        AmendAuditPopulation value => new AmendAuditPopulationHandler(fixture.Mutations)
            .HandleAsync(PopulationContext(fixture, value, metadata), CancellationToken.None),
        _ => throw new InvalidOperationException()
    };
    static Cntryl.Portia.RequestContext<T> PopulationContext<T>(Fixture fixture, T request, RequestMetadata metadata) where T : IRequestBase =>
        new(request, new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", fixture.UserId.ToString()),
                new Claim("email", "compliance-lead@example.test")], "BdgrzSession")),
            new HttpInvocation("POST", "/test", "/test", "audit-population-native-test"), metadata));
}
