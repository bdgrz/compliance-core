using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.Rules.AuditPackageRules;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;

public sealed class AuditPackageTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid BaselineId = Uuid.CreateVersion4();
    static readonly Uuid PackageId = Uuid.CreateVersion4();
    static readonly Uuid LeadId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ArchiveArtifactId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldCreateDeterministicManifestGivenSameFrozenScopeAndSelection()
    {
        // Arrange
        var entries = Entries();

        // Act
        var package = NewPackage(PackageId, entries);
        var replay = NewPackage(PackageId, entries.Reverse().ToArray());

        // Assert
        Assert.Equal(package.Current!.Versions[0].Manifest!.ManifestSha256,
            replay.Current!.Versions[0].Manifest!.ManifestSha256);
        Assert.Equal(package.Current.Versions[0].Manifest.AuditPackageManifestId,
            replay.Current.Versions[0].Manifest.AuditPackageManifestId);
        Assert.Equal(entries.Count, package.Current.Versions[0].Manifest.Entries.Count);
        Assert.Contains("/control_matrix/", package.Current.Versions[0].Manifest.Entries[0].RelativePath);
        Assert.Contains(package.Current.Versions[0].Manifest.Entries,
            entry => entry.Source.Authorship == "product_calculated");
        Assert.Contains(package.Current.Versions[0].Manifest.Entries,
            entry => entry.Source.Authorship == "management");

        var changed = NewPackage(PackageId, [entries[0] with { SourceVersion = 2 }, .. entries.Skip(1)]);
        Assert.NotEqual(package.Current.Versions[0].Manifest.ManifestSha256,
            changed.Current!.Versions[0].Manifest!.ManifestSha256);

        var changedSourceVersionId = NewPackage(PackageId,
            [entries[0] with { SourceVersionId = Uuid.CreateVersion4() }, .. entries.Skip(1)]);
        Assert.NotEqual(package.Current.Versions[0].Manifest.ManifestSha256,
            changedSourceVersionId.Current!.Versions[0].Manifest!.ManifestSha256);
    }

    [Fact]
    public void ShouldRejectPartialBuildAndRetainRetryableFailureGivenGeneratedPackage()
    {
        // Arrange
        var package = NewPackage(PackageId, Entries());
        Assert.Null(package.RequestBuild(1, "attempt-1", LeadId, "Lead", Now.AddMinutes(1)));
        var firstAttempt = Assert.Single(package.Current!.Versions[0].BuildAttempts);
        Assert.Null(package.RecordBuildProgress(2, firstAttempt.BuildAttemptId, 1, 2,
            null, "build-worker", Now.AddMinutes(2)));

        // Act
        var partialOutput = BuildOutput(package, package.Current!.Versions[0].Manifest!.Entries.Take(1).ToArray());
        var partialResult = package.RecordBuildResult(3, firstAttempt.BuildAttemptId,
            partialOutput,
            null, "build-worker", Now.AddMinutes(3));
        var repeatedPartialResult = package.RecordBuildResult(3, firstAttempt.BuildAttemptId,
            partialOutput, null, "build-worker", Now.AddMinutes(3));

        // Assert
        Assert.Null(partialResult);
        Assert.Null(repeatedPartialResult);
        Assert.Equal("failed", package.Current!.Versions[0].BuildAttempts[0].Status);
        Assert.Equal("build_failed", package.Current.Status);
        Assert.Null(package.Current.Versions[0].BuildAttempts[0].ArchiveArtifactId);

        Assert.Null(package.RequestBuild(4, "attempt-2", LeadId, "Lead", Now.AddMinutes(3)));
        var retry = package.Current.Versions[0].BuildAttempts[^1];
        Assert.Null(package.RecordBuildResult(5, retry.BuildAttemptId,
            BuildOutput(package, package.Current.Versions[0].Manifest!.Entries), null,
            "build-worker", Now.AddMinutes(4)));
        Assert.Equal("built", package.Current.Status);
        Assert.Equal(2, package.Current.Versions[0].BuildAttempts.Count);
    }

    [Fact]
    public void ShouldTreatSameBuildKeyAndActorAsRetryGivenLaterRequestTime()
    {
        // Arrange
        var package = NewPackage(PackageId, Entries());
        var first = package.RequestBuild(package.Revision, "same-key", LeadId, "Lead",
            Now.AddMinutes(1));
        var revisionAfterFirst = package.Revision;

        // Act
        var retry = package.RequestBuild(package.Revision, "same-key", LeadId, "Lead (renamed)",
            Now.AddMinutes(5));

        // Assert
        Assert.Null(first);
        Assert.Null(retry);
        Assert.Equal(revisionAfterFirst, package.Revision);
        Assert.Single(package.Current!.Versions[0].BuildAttempts);
    }

    [Fact]
    public void ShouldRequireExactManifestValidationOverrideAndDisclosureGivenBuiltPackage()
    {
        // Arrange
        var package = BuiltPackage();
        var manifest = package.Current!.Versions[0].Manifest!;
        var finding = new AuditPackageValidationFinding(Uuid.CreateVersion4(), "stale_source",
            "error", true, true, "One source was superseded after the selected snapshot.");

        // Act
        Assert.Null(package.RecordValidation(package.Revision, 1, "validation-1", [finding],
            ReviewerId, "Reviewer", Now.AddMinutes(4)));
        var beforeOverride = package.Current!.Versions[0].Validations[^1];
        var deliveryBeforeOverride = package.RecordDelivery(package.Revision, 1,
            "auditor@example.test", "transfer-1", LeadId, "Lead", Now.AddMinutes(5));
        Assert.Null(package.ApproveValidationOverride(package.Revision, 1, finding.FindingId,
            "The engagement accepted this stale source and the limitation is disclosed.",
            LeadId, "Lead", Now.AddMinutes(6)));
        var deliveryBeforeDisclosure = package.RecordDelivery(package.Revision, 1,
            "auditor@example.test", "transfer-1", LeadId, "Lead", Now.AddMinutes(7));
        Assert.Null(package.ApproveDisclosure(package.Revision, 1, "auditor@example.test",
            "Type I examination", "Approved exact package version", LeadId, "Lead",
            Now.AddMinutes(8)));
        var wrongRecipient = package.RecordDelivery(package.Revision, 1,
            "other-auditor@example.test", "transfer-1", LeadId, "Lead", Now.AddMinutes(9));
        var delivered = package.RecordDelivery(package.Revision, 1,
            "auditor@example.test", "transfer-1", LeadId, "Lead", Now.AddMinutes(10));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(deliveryBeforeOverride).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(deliveryBeforeDisclosure).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(wrongRecipient).Code);
        Assert.Equal("blocked", beforeOverride.Status);
        Assert.Equal("passed_with_overrides", package.Current!.Versions[0].Validations[^1].Status);
        Assert.NotNull(manifest);
        Assert.Null(delivered);
        var delivery = Assert.Single(package.Current.Versions[0].Deliveries);
        Assert.Equal(manifest.AuditPackageManifestId, delivery.ManifestId);
        Assert.Equal("auditor@example.test", delivery.Recipient);
        Assert.Equal(package.Current.Versions[0].BuildAttempts[^1].ArchiveSha256,
            delivery.ArchiveSha256);
    }

    [Fact]
    public void ShouldPreserveDeliveredVersionExactlyGivenAmendment()
    {
        // Arrange
        var package = BuiltPackage();
        Assert.Null(package.RecordValidation(package.Revision, 1, "validation-clean", [],
            ReviewerId, "Reviewer", Now.AddMinutes(4)));
        Assert.Null(package.ApproveDisclosure(package.Revision, 1, "auditor@example.test",
            "Type I examination", "Approved original package", LeadId, "Lead",
            Now.AddMinutes(5)));
        Assert.Null(package.RecordDelivery(package.Revision, 1, "auditor@example.test",
            "transfer-original", LeadId, "Lead", Now.AddMinutes(6)));
        var deliveredVersion = package.Current!.Versions[0];
        var deliveredManifestId = deliveredVersion.Manifest.AuditPackageManifestId;
        var deliveredManifestSha256 = deliveredVersion.Manifest.ManifestSha256;
        var deliveredArchiveSha256 = deliveredVersion.BuildAttempts[^1].ArchiveSha256;
        var originalDelivery = Assert.Single(deliveredVersion.Deliveries);

        // Act
        Assert.Null(package.Amend(package.Revision, "Include the corrected control matrix",
            Uuid.CreateVersion4(), new string('c', 64), [.. Entries(), Entries()[0] with
            { SourceVersion = 2, ContentSha256 = new string('d', 64) }], LeadId, "Lead",
            Now.AddMinutes(7)));

        // Assert
        Assert.Equal(2, package.Current!.CurrentVersion);
        Assert.Equal("draft", package.Current.Status);
        Assert.Equal(deliveredVersion.PackageVersion, package.Current.Versions[0].PackageVersion);
        Assert.Equal("delivered", package.Current.Versions[0].Status);
        Assert.Equal(deliveredManifestId, package.Current.Versions[0].Manifest.AuditPackageManifestId);
        Assert.Equal(deliveredManifestSha256, package.Current.Versions[0].Manifest.ManifestSha256);
        Assert.Equal(originalDelivery, Assert.Single(package.Current.Versions[0].Deliveries));
        Assert.Equal(deliveredArchiveSha256,
            package.Current.Versions[0].Deliveries[0].ArchiveSha256);
        Assert.Equal(2, package.Current.Versions[1].PackageVersion);
        Assert.Equal(1, package.Current.Versions[1].AmendsVersion);
        Assert.NotEqual(package.Current.Versions[0].Manifest!.ManifestSha256,
            package.Current.Versions[1].Manifest!.ManifestSha256);
    }

    [Fact]
    public async Task ShouldReplayCompletePackageHistoryGivenEvents()
    {
        // Arrange
        var package = BuiltPackage();
        Assert.Null(package.RecordValidation(package.Revision, 1, "validation-clean", [],
            ReviewerId, "Reviewer", Now.AddMinutes(4)));
        Assert.Null(package.ApproveDisclosure(package.Revision, 1, "auditor@example.test",
            "Type I examination", "Approved exact version", LeadId, "Lead", Now.AddMinutes(5)));
        Assert.Null(package.RecordDelivery(package.Revision, 1, "auditor@example.test",
            "transfer-1", LeadId, "Lead", Now.AddMinutes(6)));
        Assert.Null(package.Amend(package.Revision, "Add a late management response",
            Uuid.CreateVersion4(), new string('e', 64), Entries(), LeadId, "Lead",
            Now.AddMinutes(7)));

        // Act
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        await PackageSeeds.WriteAsync(reader, writer, events, content, package.Current!);
        var replay = await new AuditPackageReader(reader, content, events).ReadAsync(TenantId, ProgramId, PackageId,
            CancellationToken.None);
        var packageState = package.Current!;
        var replayedState = replay.View!;

        // Assert
        Assert.Equal(packageState.Revision, replayedState.Revision);
        Assert.Equal(packageState.Status, replayedState.Status);
        Assert.Equal(packageState.Versions[1].Manifest.ManifestSha256,
            replayedState.Versions[1].Manifest.ManifestSha256);
        Assert.Equal(2, replayedState.Versions.Count);
        Assert.Equal("transfer-1", Assert.Single(replayedState.Versions[0].Deliveries).DeliveryReference);
    }

    [Fact]
    public void ShouldRejectStaleRevisionAndChangedCreateRetryGivenPackage()
    {
        // Arrange
        var package = NewPackage(PackageId, Entries());

        // Act
        var stale = package.RequestBuild(0, "attempt-1", LeadId, "Lead", Now.AddMinutes(1));
        var changedRetry = package.Create(Content(Entries().Skip(1).ToArray()), LeadId,
            "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(changedRetry).Code);
    }

    [Fact]
    public void ShouldRejectDuplicateManifestPathsGivenDifferentSourceRecords()
    {
        // Arrange
        var entries = Entries().ToArray();
        entries[1] = entries[1] with
        {
            Section = entries[0].Section,
            StableId = entries[0].StableId,
            SourceVersion = entries[0].SourceVersion,
            Extension = entries[0].Extension,
            SourceRecordId = Uuid.CreateVersion4(),
        };
        var package = new AuditPackageAggregate(TenantId, ProgramId, Uuid.CreateVersion4());

        // Act
        var failure = package.Create(Content(entries), LeadId, "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, package.Revision);
    }

    [Fact]
    public void ShouldKeepNonOverridableFindingBlockedGivenBuildAndValidation()
    {
        // Arrange
        var package = BuiltPackage();
        var finding = new AuditPackageValidationFinding(Uuid.CreateVersion4(), "missing_record",
            "error", true, false, "A required record is missing.");
        Assert.Null(package.RecordValidation(package.Revision, 1, "validation-blocked",
            [finding], ReviewerId, "Reviewer", Now.AddMinutes(4)));

        // Act
        var overrideFailure = package.ApproveValidationOverride(package.Revision, 1,
            finding.FindingId, "We accept this gap", LeadId, "Lead", Now.AddMinutes(5));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(overrideFailure).Code);
        Assert.Equal("blocked", package.Current!.Versions[0].Validations[^1].Status);
        Assert.Empty(package.Current.Versions[0].Validations[^1].Overrides);
    }

    [Fact]
    public void ShouldUseSameRootForFrozenTypeIIPeriodPackagesGivenCloseSnapshot()
    {
        // Arrange
        var package = new AuditPackageAggregate(TenantId, ProgramId, Uuid.CreateVersion4());
        var closeSnapshotId = Uuid.CreateVersion4();
        var periodId = Uuid.CreateVersion4();
        var content = new AuditPackageContent(ExaminationId, "type_ii",
            "type_ii_close_snapshot", closeSnapshotId, new string('9', 64),
            "Type II package", Entries(), periodId);

        // Act
        var created = package.Create(content, LeadId, "Lead", Now);

        // Assert
        Assert.Null(created);
        Assert.Equal("type_ii", package.Current!.PackageKind);
        Assert.Equal("type_ii_close_snapshot", package.Current.Versions[0].SourceSnapshotKind);
        Assert.Equal(closeSnapshotId, package.Current.Versions[0].Manifest.SourceSnapshotId);
        Assert.Equal(periodId, package.Current.Versions[0].SourcePeriodId);
    }

    [Fact]
    public void ShouldRequireOwningPeriodIdentityGivenNewTypeIIPackage()
    {
        // Arrange
        var package = new AuditPackageAggregate(TenantId, ProgramId, Uuid.CreateVersion4());
        var content = new AuditPackageContent(ExaminationId, "type_ii",
            "type_ii_close_snapshot", Uuid.CreateVersion4(), new string('9', 64),
            "Type II package", Entries());

        // Act
        var created = package.Create(content, LeadId, "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(created).Code);
        Assert.Equal(0, package.Revision);
    }

    static AuditPackageAggregate BuiltPackage()
    {
        var package = NewPackage(PackageId, Entries());
        Assert.Null(package.RequestBuild(package.Revision, "attempt-main", LeadId, "Lead",
            Now.AddMinutes(1)));
        var attempt = package.Current!.Versions[0].BuildAttempts[0];
        Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
            BuildOutput(package, package.Current.Versions[0].Manifest!.Entries), null,
            "build-worker", Now.AddMinutes(2)));
        return package;
    }

    static AuditPackageAggregate NewPackage(Uuid packageId,
        IReadOnlyList<AuditPackageSelectionEntry> entries)
    {
        var package = new AuditPackageAggregate(TenantId, ProgramId, packageId);
        Assert.Null(package.Create(Content(entries), LeadId, "Lead", Now));
        return package;
    }

    static AuditPackageContent Content(IReadOnlyList<AuditPackageSelectionEntry> entries) =>
        new(ExaminationId, "type_i", "type_i_baseline", BaselineId, new string('a', 64),
            "Type I package", entries);

    static IReadOnlyList<AuditPackageSelectionEntry> Entries() =>
    [
        new("control_matrix", "control-17", "control", Uuid.CreateVersion5(PackageId,
            "control-17"), 3, "csv", new string('b', 64), 512, "product_calculated"),
        new("evidence_index", "artifact-9", "evidence_artifact", Uuid.CreateVersion5(PackageId,
            "artifact-9"), 2, "csv", new string('c', 64), 1024, "management"),
    ];

    static AuditPackageBuildOutput BuildOutput(AuditPackageAggregate package,
        IReadOnlyList<AuditPackageManifestEntryView> generatedEntries) =>
        new(package.Current!.Versions[0].Manifest!.AuditPackageManifestId,
            package.Current.Versions[0].Manifest.ManifestSha256, generatedEntries,
            ArchiveArtifactId, new string('f', 64), 4096, null);
}
