using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
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
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Handlers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class RecordExaminationOutcomeHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordAuditorOutcomeGivenExactDeliveredPackageAndAvailableReport()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        var package = await SeedDeliveredPackageAsync(fixture, packageId);
        var artifactId = await SeedReportArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var request = Request(fixture, packageId, artifactId);
        var handler = Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)));

        // Act
        var result = await handler.HandleAsync(Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditOutcome(fixture.TenantId,
            fixture.ProgramId, request.OutcomeId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var recorded = saved.Current!.Content;
        Assert.Equal(fixture.ExaminationId, recorded.ExaminationId);
        Assert.Equal("type_i", recorded.ScopeKind);
        Assert.Equal(packageId, recorded.PackageId);
        Assert.Equal(1, recorded.PackageVersion);
        Assert.Equal(package.Manifest.AuditPackageManifestId, recorded.PackageManifestId);
        Assert.Equal(package.Manifest.ManifestSha256, recorded.PackageManifestSha256);
        Assert.Equal("auditor", recorded.SourceKind);
        Assert.Equal("qualified", recorded.OutcomeCode);
        Assert.Equal("The auditor issued a qualified opinion.", recorded.OutcomeSummary);
        Assert.Equal("report-2026-09", recorded.ReportReference);
        Assert.Equal(artifactId, recorded.ReportArtifactId);
        Assert.Equal(new string('c', 64), recorded.ReportArtifactSha256);
    }

    [Fact]
    public async Task ShouldRejectUndeliveredPackageGivenAuditorOutcome()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        var baseline = await fixture.SeedBaselineAsync();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var request = Request(fixture, packageId, null);

        // Act
        var result = await Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)))
            .HandleAsync(Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditOutcome(fixture.TenantId,
            fixture.ProgramId, request.OutcomeId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectUnavailableReportArtifactGivenDeliveredPackage()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        await SeedDeliveredPackageAsync(fixture, packageId);
        var artifactId = await SeedReportArtifactAsync(fixture, EvidenceInspectionOutcome.Malware);
        var request = Request(fixture, packageId, artifactId);

        // Act
        var result = await Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)))
            .HandleAsync(Context(fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditOutcome(fixture.TenantId,
            fixture.ProgramId, request.OutcomeId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectOutcomeIssuedBeforePackageDeliveryGivenDeliveredPackage()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        await SeedDeliveredPackageAsync(fixture, packageId);
        var request = Request(fixture, packageId, null) with
        { IssuedOn = new DateOnly(2026, 9, 30) };

        // Act
        var result = await Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)))
            .HandleAsync(Context(fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectFutureOutcomeDateGivenDeliveredPackage()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        await SeedDeliveredPackageAsync(fixture, packageId);
        var request = Request(fixture, packageId, null) with
        { IssuedOn = new DateOnly(2026, 10, 19) };

        // Act
        var result = await Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)))
            .HandleAsync(Context(fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectPackageFromDifferentExaminationGivenAuditorOutcome()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        await SeedDeliveredPackageAsync(fixture, packageId);
        var request = Request(fixture, packageId, null) with
        { ExaminationId = Uuid.CreateVersion4() };

        // Act
        var result = await Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)))
            .HandleAsync(Context(fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldTreatExactOutcomeRetryAsIdempotentGivenLaterHandlerTime()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var packageId = Uuid.CreateVersion4();
        await SeedDeliveredPackageAsync(fixture, packageId);
        var request = Request(fixture, packageId, null);
        var context = Context(fixture.UserId, request);
        var handler = Handler(fixture, new FixedTimeProvider(Now.AddMinutes(8)));
        Assert.True((await handler.HandleAsync(context, CancellationToken.None)).IsSuccess);

        // Act
        var retry = await Handler(fixture, new FixedTimeProvider(Now.AddMinutes(10)))
            .HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditOutcome(fixture.TenantId,
            fixture.ProgramId, request.OutcomeId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now.AddMinutes(8), saved.Current!.RecordedAt);
    }

    static RecordExaminationOutcomeHandler Handler(AmendAuditPackageHandlerTests.Fixture fixture,
        TimeProvider clock) => new(fixture.Executor, fixture.Reader, fixture.Packages, clock);

    static RecordExaminationOutcome Request(AmendAuditPackageHandlerTests.Fixture fixture,
        Uuid packageId, Uuid? reportArtifactId) => new(fixture.TenantId, fixture.ProgramId,
        fixture.ExaminationId, Uuid.CreateVersion4(), packageId, 1, "qualified",
        "The auditor issued a qualified opinion.", "report-2026-09", reportArtifactId,
        new DateOnly(2026, 10, 18));

    static async Task<Uuid> SeedReportArtifactAsync(
        AmendAuditPackageHandlerTests.Fixture fixture, EvidenceInspectionOutcome inspection)
    {
        var artifactId = Uuid.CreateVersion4();
        var artifact = new EvidenceArtifact(fixture.TenantId, artifactId);
        Assert.True(artifact.Register(new EvidenceArtifactContent("Issued SOC 2 report", null,
                "document", "Auditor issued report", Now.AddMinutes(5),
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "confidential"),
            new string('c', 64), 512,
            ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Audit manager"),
            Now.AddMinutes(5)).IsSuccess);
        Assert.Null(artifact.RecordInspection(inspection, Now.AddMinutes(6)));
        await fixture.Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));
        return artifactId;
    }

    static Cntryl.Portia.RequestContext<RecordExaminationOutcome> Context(Uuid userId,
        RecordExaminationOutcome request) => new(request,
        new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "audit-manager@example.test")], "BdgrzSession")),
            new HttpInvocation("POST", "/api/v1/tenants/test/programs/test/examinations/outcomes",
                "/api/v1/tenants/test/programs/test/examinations/outcomes", "audit-outcome-test")));

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    static async Task<AuditPackageVersionView> SeedDeliveredPackageAsync(
        AmendAuditPackageHandlerTests.Fixture fixture, Uuid packageId)
    {
        var baseline = await fixture.SeedBaselineAsync();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var package = await fixture.ReadPackageAsync(packageId);
        var actorMemberId = RbacIds.Member(fixture.TenantId, fixture.UserId);
        Assert.Null(package.RecordValidation(package.Revision, 1, "outcome-test", [],
            actorMemberId, "Audit manager", new DateTimeOffset(2026, 10, 18, 17, 4, 0,
                TimeSpan.Zero)));
        Assert.Null(package.ApproveDisclosure(package.Revision, 1, "auditor@example.test",
            "Type I examination", "The exact package is approved for auditor delivery.",
            actorMemberId, "Audit manager", new DateTimeOffset(2026, 10, 18, 17, 5, 0,
                TimeSpan.Zero)));
        Assert.Null(package.RecordDelivery(package.Revision, 1, "auditor@example.test",
            "delivery-reference-1", actorMemberId, "Audit manager",
            new DateTimeOffset(2026, 10, 18, 17, 6, 0, TimeSpan.Zero)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.ContentStore, package.Current!);
        return package.Current!.Versions[0];
    }
}
