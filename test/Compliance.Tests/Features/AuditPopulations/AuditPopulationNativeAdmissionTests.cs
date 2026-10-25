using System.Security.Claims;
using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.AuditPopulation;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

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
