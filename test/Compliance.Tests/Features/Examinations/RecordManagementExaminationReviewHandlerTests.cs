using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Handlers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Tests.Testing;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Tests.Features.AuditPackages;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class RecordManagementExaminationReviewHandlerTests
{
    [Fact]
    public async Task ShouldRequirePersonalManagementActorGivenCompleteSignOffIntent()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var content = Content();
        var request = new RecordManagementExaminationReview(fixture.TenantId,
            fixture.ProgramId, content.ExaminationId, Uuid.CreateVersion4(), content);
        var handler = new RecordManagementExaminationReviewHandler(fixture.Executor,
            fixture.Reader, fixture.Packages, fixture.Periods, new ManagementExecutiveAuthority(fixture.Reader,
                new DenyPermissions()), new FixedTimeProvider(DateTimeOffset.UtcNow), fixture.Examinations);
        var context = AmendAuditPackageHandlerTests.Fixture.SystemContext(request);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectUnpersistedPackageGivenAuthorizedManagementSigner()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var member = new Member(fixture.TenantId, fixture.UserId);
        Assert.True(member.Register().IsSuccess);
        await fixture.Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var memberId = RbacIds.Member(fixture.TenantId, fixture.UserId);
        var content = Content(fixture.ExaminationId, memberId);
        var signOffId = Uuid.CreateVersion4();
        var request = new RecordManagementExaminationReview(fixture.TenantId,
            fixture.ProgramId, fixture.ExaminationId, signOffId, content);
        var handler = new RecordManagementExaminationReviewHandler(fixture.Executor,
            fixture.Reader, fixture.Packages, fixture.Periods, new ManagementExecutiveAuthority(fixture.Reader,
                new AllowPermissions()), new FixedTimeProvider(DateTimeOffset.UtcNow), fixture.Examinations);

        // Act
        var result = await handler.HandleAsync(
            AmendAuditPackageHandlerTests.Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationSignOff(fixture.TenantId,
            fixture.ProgramId, signOffId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRequirePersistedExecutivePermissionGivenNamedSigner()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var member = new Member(fixture.TenantId, fixture.UserId);
        Assert.True(member.Register().IsSuccess);
        await fixture.Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var memberId = RbacIds.Member(fixture.TenantId, fixture.UserId);
        var content = Content(fixture.ExaminationId, memberId);
        var signOffId = Uuid.CreateVersion4();
        var request = new RecordManagementExaminationReview(fixture.TenantId,
            fixture.ProgramId, fixture.ExaminationId, signOffId, content);
        var handler = new RecordManagementExaminationReviewHandler(fixture.Executor,
            fixture.Reader, fixture.Packages, fixture.Periods, new ManagementExecutiveAuthority(fixture.Reader,
                new DenyPermissions()), new FixedTimeProvider(DateTimeOffset.UtcNow), fixture.Examinations);

        // Act
        var result = await handler.HandleAsync(
            AmendAuditPackageHandlerTests.Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationSignOff(fixture.TenantId,
            fixture.ProgramId, signOffId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(saved.Current);
    }

    [Fact]
    public async Task ShouldRecordExactManagementSignOffGivenApprovedAndDeliveredSourceChain()
    {
        // Arrange
        await using var fixture = new AmendAuditPackageHandlerTests.Fixture();
        var member = new Member(fixture.TenantId, fixture.UserId);
        Assert.True(member.Register().IsSuccess);
        await fixture.Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var memberId = RbacIds.Member(fixture.TenantId, fixture.UserId);
        var baseline = await fixture.SeedBaselineAsync();
        var criteria = new TypeIBaselineSourceReference("criteria", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 2, new string('c', 64));
        var control = new TypeIBaselineSourceReference("control", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 3, new string('d', 64));
        var engagement = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);
        var amendmentPreview = TypeIBaselinePreviewTestFactory.Create(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, baseline.Sources.Append(criteria).Append(control).ToArray());
        Assert.Null(engagement.Rules.AmendBaseline(engagement.Owner.Revision, Uuid.CreateVersion4(),
            amendmentPreview, [criteria.RecordId, control.RecordId], [],
            "Include the exact criteria and control inputs reviewed by management.", memberId,
            "Management executive", new DateTimeOffset(2026, 10, 18, 17, 1, 0, TimeSpan.Zero)));
        await ExaminationSeeds.WriteAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.ContentStore, engagement.Rules);
        baseline = engagement.Rules.Baseline!;
        var packageId = Uuid.CreateVersion4();
        var selectedInputs = baseline.Sources.Where(item => item.Kind is "criteria" or "control")
            .Select(item => new AuditPackageSelectionEntry("examination", item.Kind,
                item.Kind, item.RecordId, item.Revision, "json", item.ContentSha256, 128,
                "management", item.VersionId));
        await fixture.SeedBuiltPackageAsync(packageId, baseline, selectedContent:
            fixture.Content(baseline).SelectedContent.Concat(selectedInputs).ToArray());
        var package = await fixture.ReadPackageAsync(packageId);
        Assert.Null(package.RecordValidation(package.Revision, 1, "management-review-test", [],
            memberId, "Management executive", new DateTimeOffset(2026, 10, 18, 17, 4, 0,
                TimeSpan.Zero)));
        Assert.Null(package.ApproveDisclosure(package.Revision, 1, "auditor@example.test",
            "Type I examination", "The exact package is approved for auditor delivery.",
            memberId, "Management executive", new DateTimeOffset(2026, 10, 18, 17, 5, 0,
                TimeSpan.Zero)));
        Assert.Null(package.RecordDelivery(package.Revision, 1, "auditor@example.test",
            "management-review-delivery", memberId, "Management executive",
            new DateTimeOffset(2026, 10, 18, 17, 6, 0, TimeSpan.Zero)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.ContentStore, package.Current!);
        var packageVersion = package.Current!.Versions.Single();
        var baselineReferences = baseline.Sources.Select(item => new ExaminationContentReference(
            item.Kind, item.RecordId, item.VersionId, item.Revision, item.ContentSha256)).ToArray();
        var descriptionSource = baseline.Sources.First(item => item.Kind == "system_description");
        var assertionInputs = baselineReferences.Where(item => item.SourceType is "criteria" or "control")
            .ToArray();
        var assertionId = Uuid.CreateVersion4();
        var assertion = new ManagementAssertion(fixture.TenantId, fixture.ProgramId, assertionId);
        var assertionContent = new ManagementAssertionContent(fixture.ExaminationId, "type_i",
            baseline.TypeIBaselineId, baseline.ContentSha256, descriptionSource.VersionId,
            descriptionSource.ContentSha256, packageId, 1,
            packageVersion.Manifest.AuditPackageManifestId, packageVersion.Manifest.ManifestSha256,
            "management", Uuid.CreateVersion4(), "Management's assertion for this exact examination.",
            assertionInputs, [new(memberId, "ceo")]);
        Assert.Null(assertion.Create(assertionContent, Uuid.CreateVersion4(), "Assertion author",
            new DateTimeOffset(2026, 10, 18, 17, 7, 0, TimeSpan.Zero)));
        Assert.Null(assertion.Approve(assertion.Revision, 1, "approve",
            "Reviewed the exact source chain.", memberId, "ceo", "Management executive",
            new DateTimeOffset(2026, 10, 18, 17, 8, 0, TimeSpan.Zero)));
        await fixture.Writer.SaveAsync(assertion, new RequestDispatchContext(RequestActor.System));
        var assertionVersion = assertion.Current!.CurrentVersion!;

        var letterId = Uuid.CreateVersion4();
        var letter = new ManagementRepresentationLetter(fixture.TenantId, fixture.ProgramId,
            letterId);
        var originalLetterArtifact = await SeedAvailableArtifactAsync(fixture, memberId,
            "Original auditor representation letter", new DateTimeOffset(2026, 10, 18, 17, 8, 0,
                TimeSpan.Zero), 'a');
        var signedLetterArtifact = await SeedAvailableArtifactAsync(fixture, memberId,
            "Signed management representation letter", new DateTimeOffset(2026, 10, 18, 17, 9, 0,
                TimeSpan.Zero), 'b');
        var letterContent = new ManagementRepresentationLetterContent(fixture.ExaminationId,
            "type_i", packageId, 1, packageVersion.Manifest.AuditPackageManifestId,
            packageVersion.Manifest.ManifestSha256, assertionId, 1,
            assertionVersion.ContentSha256, "auditor", "auditor-letter-reference",
            originalLetterArtifact.Id, originalLetterArtifact.Sha256, new DateOnly(2026, 10, 18),
            [new(memberId, "ceo")]);
        Assert.Null(letter.Record(letterContent, memberId, "Management executive",
            new DateTimeOffset(2026, 10, 18, 17, 9, 0, TimeSpan.Zero)));
        Assert.Null(letter.Sign(letter.Revision, memberId, signedLetterArtifact.Id,
            signedLetterArtifact.Sha256, new DateTimeOffset(2026, 10, 18, 17, 10, 0, TimeSpan.Zero)));
        await fixture.Writer.SaveAsync(letter, new RequestDispatchContext(RequestActor.System));

        var outcomeId = Uuid.CreateVersion4();
        var outcome = new AuditOutcome(fixture.TenantId, fixture.ProgramId, outcomeId);
        var outcomeContent = new AuditOutcomeContent(fixture.ExaminationId, "type_i",
            baseline.TypeIBaselineId, baseline.ContentSha256, packageId, 1,
            packageVersion.Manifest.AuditPackageManifestId, packageVersion.Manifest.ManifestSha256,
            "auditor", "qualified", "The auditor issued a qualified opinion.",
            "report-2026-09", null, null, new DateOnly(2026, 10, 18));
        Assert.Null(outcome.Record(outcomeContent, memberId, "Audit manager",
            new DateTimeOffset(2026, 10, 18, 17, 11, 0, TimeSpan.Zero)));
        await fixture.Writer.SaveAsync(outcome, new RequestDispatchContext(RequestActor.System));

        var content = new ExaminationSignOffContent(fixture.ExaminationId, "type_i",
            baseline.TypeIBaselineId, baseline.ContentSha256, packageId, 1,
            packageVersion.Manifest.AuditPackageManifestId, packageVersion.Manifest.ManifestSha256,
            assertionId, 1, assertionVersion.ContentSha256, letterId,
            Digest(letter.Current!.Content), outcomeId, outcome.Current!.ContentSha256,
            [], [], [], "approved", "Reviewed the exact delivered package and auditor outcome.",
            [new(memberId, "ceo")]);
        var request = new RecordManagementExaminationReview(fixture.TenantId,
            fixture.ProgramId, fixture.ExaminationId, Uuid.CreateVersion4(), content);
        var handler = new RecordManagementExaminationReviewHandler(fixture.Executor,
            fixture.Reader, fixture.Packages, fixture.Periods, new ManagementExecutiveAuthority(fixture.Reader,
                new AllowPermissions()), new FixedTimeProvider(
                new DateTimeOffset(2026, 10, 18, 17, 12, 0, TimeSpan.Zero)), fixture.Examinations);

        // Act
        var result = await handler.HandleAsync(
            AmendAuditPackageHandlerTests.Fixture.Context(fixture.UserId, request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationSignOff(fixture.TenantId,
            fixture.ProgramId, request.SignOffId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("approved", saved.Current!.Content.Decision);
        Assert.Equal(packageVersion.Manifest.AuditPackageManifestId,
            saved.Current.Content.PackageManifestId);
        Assert.Equal(outcomeId, saved.Current.Content.AuditOutcomeId);
        Assert.Equal(memberId, saved.Current.SignedOffByMemberId);
        var retry = await new RecordManagementExaminationReviewHandler(fixture.Executor,
                fixture.Reader, fixture.Packages, fixture.Periods, new ManagementExecutiveAuthority(fixture.Reader,
                    new AllowPermissions()), new FixedTimeProvider(
                    new DateTimeOffset(2026, 10, 18, 17, 14, 0, TimeSpan.Zero)), fixture.Examinations)
            .HandleAsync(AmendAuditPackageHandlerTests.Fixture.Context(fixture.UserId, request),
                CancellationToken.None);
        var tamperedRequest = request with
        {
            SignOffId = Uuid.CreateVersion4(),
            Content = content with { AuditOutcomeContentSha256 = new string('f', 64) },
        };
        var tampered = await handler.HandleAsync(
            AmendAuditPackageHandlerTests.Fixture.Context(fixture.UserId, tamperedRequest),
            CancellationToken.None);
        var tamperedState = await fixture.Reader.HydrateAsync(new ExaminationSignOff(
            fixture.TenantId, fixture.ProgramId, tamperedRequest.SignOffId));

        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, tampered.Error?.Kind);
        Assert.Null(tamperedState.Current);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 17, 12, 0, TimeSpan.Zero),
            saved.Current.SignedOffAt);
    }

    static ExaminationSignOffContent Content(Uuid? examinationId = null, Uuid? approverId = null) =>
        new(examinationId ?? Uuid.CreateVersion4(), "type_i",
        Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), 1,
        Uuid.CreateVersion4(), new string('b', 64), Uuid.CreateVersion4(), 1,
        new string('c', 64), Uuid.CreateVersion4(), new string('d', 64), Uuid.CreateVersion4(),
        new string('e', 64), [], [], [], "approved", "Reviewed the exact examination records.",
        [new(approverId ?? Uuid.CreateVersion4(), "ceo")]);

    static string Digest<T>(T value) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    static async Task<(Uuid Id, string Sha256)> SeedAvailableArtifactAsync(
        AmendAuditPackageHandlerTests.Fixture fixture, Uuid memberId, string title,
        DateTimeOffset registeredAt, char digestCharacter)
    {
        var artifactId = Uuid.CreateVersion4();
        var digest = new string(digestCharacter, 64);
        var artifact = new EvidenceArtifact(fixture.TenantId, artifactId);
        Assert.True(artifact.Register(new EvidenceArtifactContent(title, null, "document", title,
                registeredAt, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "confidential"),
            digest, 512, ActorReference.ForMember(memberId, "Management executive"), registeredAt)
            .IsSuccess);
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean,
            registeredAt.AddMinutes(1)));
        await fixture.Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));
        return (artifactId, digest);
    }

    sealed class DenyPermissions : IAccessGrantPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(false, new HashSet<Uuid>()));
    }

    sealed class AllowPermissions : IAccessGrantPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission == RbacPermissions.ManagementExecutiveCeo);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(false, new HashSet<Uuid>()));
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
