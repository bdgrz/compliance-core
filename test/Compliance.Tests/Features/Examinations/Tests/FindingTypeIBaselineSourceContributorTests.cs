using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
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
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class FindingTypeIBaselineSourceContributorTests
{
    [Fact]
    public async Task ShouldCaptureFindingWithItsActionHistoryGivenFactsInsideCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var contributor = Contributor(fixture);

        // Act
        var result = await contributor.ReadAsync(Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)),
            new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("findings", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("finding", source.Kind);
        Assert.Equal(finding.FindingId, source.RecordId);
        Assert.Equal(2, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(finding.FindingId, "finding_v1:2"), source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
    }

    [Fact]
    public async Task ShouldChangeDigestGivenLaterFactInsideCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var contributor = Contributor(fixture);
        var engagement = Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));
        var before = await contributor.ReadAsync(engagement, new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []),
            CancellationToken.None);

        // Act
        await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, 1, "critical", fixture.OwnerMemberId, fixture.Today.AddDays(3), "All",
            null, "Escalated."));
        var after = await contributor.ReadAsync(engagement, new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []),
            CancellationToken.None);
        var again = await contributor.ReadAsync(engagement, new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, Assert.Single(before.Value.Sources).Revision);
        var revised = Assert.Single(after.Value.Sources);
        Assert.Equal(2, revised.Revision);
        Assert.NotEqual(Assert.Single(before.Value.Sources).ContentSha256, revised.ContentSha256);
        Assert.Equal(revised.ContentSha256, Assert.Single(again.Value.Sources).ContentSha256);
    }

    [Fact]
    public async Task ShouldExcludeFindingGivenEnvelopeRecordedAfterCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var contributor = Contributor(fixture);

        // Act
        var result = await contributor.ReadAsync(Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)),
            new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
    }

    [Fact]
    public async Task ShouldArchiveExactFindingActionAndEvidenceBytesGivenProductionSourcePublication()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var directory = Directory.CreateTempSubdirectory("bdgrz-finding-source-archive-").FullName;
        try
        {
            var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(directory, null,
                ArtifactContentLimits.MaxContentLength), TimeProvider.System);
            var publisher = new PublishedTypeIBaselinePreviewReader(
                new TypeIBaselinePreviewReader([Contributor(fixture)]), artifacts, content);
            var preview = await publisher.ReadAsync(Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));
            Assert.True(preview.IsSuccess, preview.Error?.Message);
            var source = Assert.Single(preview.Value.Sources);
            Assert.NotNull(source.Content);
            var selected = new AuditPackageSelectionEntry("findings", "F-01", source.Kind, source.RecordId,
                source.Revision, source.Content.FileExtension, source.ContentSha256, source.Content.Bytes,
                "product_calculated", source.VersionId, source.Content);
            var manifest = new AuditPackageManifestView(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), preview.Value.ContentSha256, [finding.FindingId],
                [new AuditPackageManifestEntryView(selected, "findings/F-01." + selected.Extension)],
                new string('a', 64), DateTimeOffset.UtcNow);

            // Act
            var built = await new AuditPackageArchiveBuilder(artifacts).BuildAsync(fixture.TenantId, manifest,
                CancellationToken.None);
            Assert.True(built.IsSuccess, built.Error?.Message);
            await using var stored = await artifacts.OpenVerifiedAsync(new ArtifactContentReference(fixture.TenantId,
                built.Value.ArchiveSha256!, built.Value.ArchiveBytes!.Value));
            using var zip = new ZipArchive(stored!, ZipArchiveMode.Read);
            var entry = Assert.Single(zip.Entries);
            await using var payload = entry.Open();
            using var envelope = await JsonDocument.ParseAsync(payload);
            var proof = Convert.FromBase64String(envelope.RootElement.GetProperty("source_base64").GetString()!);
            var recovered = new List<string>();
            foreach (var blob in envelope.RootElement.GetProperty("content_blobs").EnumerateArray())
            {
                var reference = blob.GetProperty("reference").Deserialize(ComplianceCoreJsonContext.Default.ImmutableContentRef)!;
                var bytes = Convert.FromBase64String(blob.GetProperty("base64").GetString()!);
                Assert.Equal(reference.Bytes, bytes.LongLength);
                Assert.Equal(reference.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                Assert.Equal((await content.ReadVerifiedAsync(fixture.TenantId.ToString(), reference)).ToArray(), bytes);
                recovered.Add(Encoding.UTF8.GetString(bytes));
            }

            // Assert
            Assert.Equal(source.ContentSha256, Convert.ToHexStringLower(SHA256.HashData(proof)));
            Assert.StartsWith("bdgrz.snapshot.source.finding.v1\0", Encoding.UTF8.GetString(proof), StringComparison.Ordinal);
            Assert.Equal(source.ContentSha256, envelope.RootElement.GetProperty("logical_content_sha256").GetString());
            Assert.Equal("application/vnd.bdgrz.finding-snapshot-source.v1+json", source.Content.MediaType);
            Assert.Equal(source.Content.Bytes, entry.Length);
            Assert.Contains(recovered, body => body.Contains("Remove stale VPN accounts.", StringComparison.Ordinal));
            Assert.Contains(recovered, body => body.Contains("Removal ticket", StringComparison.Ordinal));
            Assert.Contains(recovered, body => body.Contains(fixture.ControlId.ToString(), StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyUnverifiableFindingBusinessBytesGivenSelectedSourceFact(bool unavailable)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        using var scope = fixture.Provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var owner = await reader.HydrateAsync(new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId));
        var contributor = new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            reader, new UnverifiableFindingContent(fixture.Content, owner.Raised!.LinksManifest, unavailable));

        // Act
        var outcome = await contributor.ReadAsync(Engagement(fixture, fixture.Today.AddDays(1)), CancellationToken.None);

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, outcome.Error?.Kind);
        Assert.Equal(unavailable, outcome.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldIgnoreUnselectedBusinessContentGivenFindingRecordedAfterCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        using var scope = fixture.Provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var owner = await reader.HydrateAsync(new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId));
        var contributor = new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            reader, new UnverifiableFindingContent(fixture.Content, owner.Raised!.LinksManifest, false));

        // Act
        var outcome = await contributor.ReadAsync(Engagement(fixture, fixture.Today.AddDays(-2)), CancellationToken.None);

        // Assert
        Assert.True(outcome.IsSuccess, outcome.Error?.Message);
        Assert.Empty(outcome.Value.Sources);
    }

    sealed class UnverifiableFindingContent(IImmutableContentStore inner, ImmutableContentRef selected,
        bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.ContentId == selected.ContentId
            ? throw (unavailable ? new ImmutableContentUnavailableException("The finding source bytes are unavailable.")
                : new ImmutableContentIntegrityException("The finding source bytes cannot be verified."))
            : inner.ReadVerifiedAsync(realm, reference, ct);
    }

    static FindingTypeIBaselineSourceContributor Contributor(OperationsFixture fixture)
    {
        var scope = fixture.Provider.CreateScope();
        return new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
    }

    static ExaminationEngagementView Engagement(OperationsFixture fixture, DateOnly typeIDate) => new(fixture.TenantId,
        fixture.ProgramId, Uuid.CreateVersion4(), "type_i", "Audit Firm", typeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(typeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), fixture.LeadMemberId, "Examiner");
}
