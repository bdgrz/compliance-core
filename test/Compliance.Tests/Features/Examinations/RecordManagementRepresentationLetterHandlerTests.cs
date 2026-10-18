using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Tests.Features.AuditPackages;
using Cntryl.Portia;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackage;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class RecordManagementRepresentationLetterHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordExactLetterGivenApprovedAssertionDeliveredPackageAndAvailableArtifact()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture);
        var handler = new RecordManagementRepresentationLetterHandler(fixture.Executor,
            fixture.Reader, new FixedTimeProvider(Now.AddMinutes(12)));
        var request = Request(fixture, sources);

        // Act
        var result = await handler.HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementRepresentationLetter(
            fixture.TenantId, fixture.ProgramId, request.LetterId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.ExaminationId, saved.Current!.Content.ExaminationId);
        Assert.Equal("type_i", saved.Current.Content.ScopeKind);
        Assert.Equal(sources.PackageId, saved.Current.Content.PackageId);
        Assert.Equal(1, saved.Current.Content.PackageVersion);
        Assert.Equal(sources.ManifestId, saved.Current.Content.PackageManifestId);
        Assert.Equal(sources.ManifestSha256, saved.Current.Content.PackageManifestSha256);
        Assert.Equal(sources.AssertionId, saved.Current.Content.AssertionId);
        Assert.Equal(sources.AssertionSha256, saved.Current.Content.AssertionContentSha256);
        Assert.Equal("auditor", saved.Current.Content.SourceKind);
        Assert.Equal("auditor-report-2026-09", saved.Current.Content.SourceReference);
        Assert.Equal(sources.ArtifactId, saved.Current.Content.OriginalArtifactId);
        Assert.Equal(new string('c', 64), saved.Current.Content.OriginalArtifactSha256);
        Assert.Equal(sources.CeoMemberId, Assert.Single(saved.Current.Content.RequiredSigners).MemberId);
        Assert.Equal("awaiting_signatures", saved.Current.Status);
    }

    [Fact]
    public async Task ShouldRejectUndeliveredPackageGivenRepresentationLetter()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture, deliverPackage: false);
        var request = Request(fixture, sources);

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementRepresentationLetter(
            fixture.TenantId, fixture.ProgramId, request.LetterId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectUnapprovedAssertionGivenRepresentationLetter()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture, approveAssertion: false);
        var request = Request(fixture, sources);

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectUnavailableOriginalArtifactGivenRepresentationLetter()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture, inspectArtifact: false);
        var request = Request(fixture, sources);

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementRepresentationLetter(
            fixture.TenantId, fixture.ProgramId, request.LetterId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
    }

    [Fact]
    public async Task ShouldRejectNonAuditorArtifactGivenRepresentationLetter()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture, artifactSource: "management-upload");
        var request = Request(fixture, sources);

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectChangedRequiredSignerSetGivenApprovedAssertion()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture);
        var request = Request(fixture, sources) with
        { RequiredSignerIds = [Uuid.CreateVersion4()] };

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectFutureLetterDateGivenApprovedAssertion()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture);
        var request = Request(fixture, sources) with
        { LetterDate = new DateOnly(2026, 10, 19) };

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectLetterDateBeforePackageDeliveryGivenApprovedAssertion()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture);
        var request = Request(fixture, sources) with
        { LetterDate = new DateOnly(2026, 9, 30) };

        // Act
        var result = await Handler(fixture).HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(
            fixture.UserId, request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldTreatSameLetterRetryAsIdempotentGivenLaterHandlerTime()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var sources = await SeedSourcesAsync(fixture);
        var request = Request(fixture, sources);
        var context = AmendAuditPackageHandlerTests.Fixture.Context(fixture.UserId, request);
        var first = await Handler(fixture).HandleAsync(context, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var retry = await new RecordManagementRepresentationLetterHandler(fixture.Executor,
                fixture.Reader, new FixedTimeProvider(Now.AddMinutes(14)))
            .HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementRepresentationLetter(
            fixture.TenantId, fixture.ProgramId, request.LetterId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now.AddMinutes(12), saved.Current!.RecordedAt);
    }

    static RecordManagementRepresentationLetterHandler Handler(
        AmendAuditPackageHandlerTests.Fixture fixture) =>
        new(fixture.Executor, fixture.Reader, new FixedTimeProvider(Now.AddMinutes(12)));

    static RecordManagementRepresentationLetter Request(
        AmendAuditPackageHandlerTests.Fixture fixture, LetterSources sources) =>
        new(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId,
            Uuid.CreateVersion4(), sources.AssertionId, sources.ArtifactId,
            "auditor-report-2026-09", new DateOnly(2026, 10, 18), [sources.CeoMemberId]);

    static async Task<LetterSources> SeedSourcesAsync(
        AmendAuditPackageHandlerTests.Fixture fixture, bool approveAssertion = true,
        bool deliverPackage = true, bool inspectArtifact = true,
        string artifactSource = "auditor")
    {
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var package = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(
            fixture.TenantId, fixture.ProgramId, packageId));
        var packageVersion = package.Current!.Versions[0];
        var actorMemberId = RbacIds.Member(fixture.TenantId, fixture.UserId);
        if (deliverPackage)
        {
            Assert.Null(package.RecordValidation(package.Revision, 1, "letter-validation", [],
                actorMemberId, "Management representative", Now.AddMinutes(4)));
            Assert.Null(package.ApproveDisclosure(package.Revision, 1, "auditor@example.test",
                "Type I examination", "The exact package is approved for auditor delivery.",
                actorMemberId, "Management representative", Now.AddMinutes(5)));
            Assert.Null(package.RecordDelivery(package.Revision, 1, "auditor@example.test",
                "letter-delivery-1", actorMemberId, "Management representative",
                Now.AddMinutes(6)));
        }
        await fixture.Writer.SaveAsync(package, new RequestDispatchContext(RequestActor.System));

        var ceoMemberId = RbacIds.Member(fixture.TenantId, Uuid.CreateVersion4());
        var assertionId = Uuid.CreateVersion4();
        var assertion = new ManagementAssertion(fixture.TenantId, fixture.ProgramId, assertionId);
        var assertionContent = new ManagementAssertionContent(fixture.ExaminationId, "type_i",
            baseline.TypeIBaselineId, baseline.ContentSha256, Uuid.CreateVersion4(),
            new string('b', 64), packageId, 1, packageVersion.Manifest.AuditPackageManifestId,
            packageVersion.Manifest.ManifestSha256, "auditor", Uuid.CreateVersion4(),
            "Management's exact description of the audited system.",
            [Reference("criteria"), Reference("control")], [new(ceoMemberId, "ceo")]);
        Assert.Null(assertion.Create(assertionContent, actorMemberId,
            "Management representative", Now.AddMinutes(7)));
        if (approveAssertion)
            Assert.Null(assertion.Approve(assertion.Revision, 1, "approve",
                "Reviewed the exact delivered package and assertion.", ceoMemberId, "ceo",
                "Chief Executive", Now.AddMinutes(8)));
        await fixture.Writer.SaveAsync(assertion, new RequestDispatchContext(RequestActor.System));

        var artifactId = Uuid.CreateVersion4();
        var artifact = new EvidenceArtifact(fixture.TenantId, artifactId);
        Assert.True(artifact.Register(new EvidenceArtifactContent("Signed management representation letter",
                null, "document", artifactSource, Now.AddMinutes(9),
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "confidential"),
            new string('c', 64), 1024,
            ActorReference.ForMember(actorMemberId, "Management representative"),
            Now.AddMinutes(9)).IsSuccess);
        if (inspectArtifact)
            Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now.AddMinutes(10)));
        await fixture.Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));

        return new LetterSources(packageId, packageVersion.Manifest.AuditPackageManifestId,
            packageVersion.Manifest.ManifestSha256, assertionId,
            assertion.Current!.CurrentVersion!.ContentSha256, artifactId, ceoMemberId);
    }

    static ExaminationContentReference Reference(string type) =>
        new(type, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('d', 64));

    sealed record LetterSources(Uuid PackageId, Uuid ManifestId, string ManifestSha256,
        Uuid AssertionId, string AssertionSha256, Uuid ArtifactId, Uuid CeoMemberId);

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
