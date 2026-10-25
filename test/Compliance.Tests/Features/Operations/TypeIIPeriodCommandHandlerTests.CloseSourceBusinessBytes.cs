using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Fact]
    public async Task ShouldArchiveExactOperatingBodiesGivenProductionNativeCloseReconciliation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedCoveredCloseReadyPeriodAsync();
        var operating = fixture.Host.Services.GetRequiredService<ControlOperationsReader>();
        var recorded = (await operating.ReadAsync(fixture.TenantId, fixture.ProgramId, CancellationToken.None))
            .ReadRecordedOccurrences(fixture.ControlId)[0];
        var plan = await ControlOperatingSeed.CurrentPlanAsync(fixture.Host.Provider, fixture.TenantId,
            fixture.ProgramId, fixture.ControlId);
        var attested = await ControlOperatingSeed.AttestAsync(fixture.Host.Provider, recorded, plan,
            new AttestationInput("complete", StartedAt, recorded.PeriodStart, recorded.PeriodEnd,
                "Operating close evidence narrative.", null,
                [new EvidenceReference(0, "external", "operating-ticket", "Operating evidence ticket", "unresolved")],
                plan.Owner), fixture.ControlOwnerId, "Operator", StartedAt);
        await ControlOperatingSeed.ReviewAsync(fixture.Host.Provider, attested, "approve", [],
            fixture.ReviewerMemberId, "Reviewer", StartedAt.AddHours(1));
        var boundary = Uuid.CreateVersion4();
        await SeedCloseEngagementAsync(fixture, boundary);
        var directory = Directory.CreateTempSubdirectory("bdgrz-operating-close-archive-").FullName;
        try
        {
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(directory, null,
                ArtifactContentLimits.MaxContentLength), fixture.CloseClock);
            var families = new PublishedTypeIBaselinePreviewReader(new TypeIBaselinePreviewReader(
                TypeIBaselineSourceFamilies.Required.Select(family => new CloseFamily(family, boundary)).ToArray()),
                artifacts, fixture.Host.Content);
            var publisher = new TypeIIPeriodCloseSourcePublication(artifacts, fixture.Host.Content);
            var history = new TypeIIPeriodCloseHistory(fixture.Events,
                new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)),
                new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Host.Content,
                    fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Content,
                new Bdgrz.Compliance.Features.Incidents.ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Host.Content,
                    fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()),
                fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>());
            var reconciliation = new TypeIIPeriodCloseReconciliation(fixture.Reader, fixture.Events, fixture.Host.Content, operating,
                fixture.Host.Services.GetRequiredService<FindingReader>(), families, history, publisher,
                new RecordedSourceReceiptCutFactory(fixture.Events, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()));
            var period = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
            var request = await CloseRequestAsync(fixture);
            var operation = RootOperation.Provenance(request.CommandId, nameof(RecordTypeIIPeriodCloseValidation),
                period.Stream, ActorReference.ForMember(fixture.ActorMemberId, "Close validator"), period.Revision,
                new string('a', 64), fixture.CloseClock.GetUtcNow());
            var captured = await reconciliation.ReadAsync(period, fixture.Context(request).Actor, fixture.ActorUserId,
                operation, CancellationToken.None);
            Assert.True(captured.IsSuccess, captured.Error?.Message);
            var publication = await publisher.ReadAsync(period.View!, captured.Value.SourceContentManifest!, CancellationToken.None);
            var source = Assert.Single(publication.Sources, value => value.SourceType == "type_ii_native_history");
            var selected = new AuditPackageSelectionEntry("period", "operating", source.SourceType, source.RecordId,
                source.Version, source.Content.FileExtension, source.LogicalSha256, source.Content.Bytes,
                "product_calculated", source.VersionId, source.Content);
            var manifest = new AuditPackageManifestView(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), new string('c', 64), [fixture.PeriodId],
                [new AuditPackageManifestEntryView(selected, "period/operating." + selected.Extension)],
                new string('a', 64), operation.AcceptedAt);

            // Act
            var built = await new AuditPackageArchiveBuilder(artifacts).BuildAsync(fixture.TenantId, manifest,
                CancellationToken.None);
            Assert.True(built.IsSuccess, built.Error?.Message);
            await using var archive = await artifacts.OpenVerifiedAsync(new ArtifactContentReference(fixture.TenantId,
                built.Value.ArchiveSha256!, built.Value.ArchiveBytes!.Value));
            using var zip = new ZipArchive(archive!, ZipArchiveMode.Read);
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
                Assert.Equal((await fixture.Host.Content.ReadVerifiedAsync(fixture.TenantId.ToString(), reference)).ToArray(), bytes);
                recovered.Add(Encoding.UTF8.GetString(bytes));
            }

            // Assert
            Assert.Equal(source.LogicalSha256, Convert.ToHexStringLower(SHA256.HashData(proof)));
            Assert.StartsWith("bdgrz.snapshot.source.type_ii_close_native_history.v1\0", Encoding.UTF8.GetString(proof), StringComparison.Ordinal);
            Assert.Equal(source.LogicalSha256, envelope.RootElement.GetProperty("logical_content_sha256").GetString());
            Assert.Equal("application/vnd.bdgrz.type-ii-close-snapshot-source.v1+json", source.Content.MediaType);
            Assert.Equal(source.Content.Bytes, entry.Length);
            Assert.Contains(recovered, value => value.Contains("Operate quarterly.", StringComparison.Ordinal));
            Assert.Contains(recovered, value => value.Contains("Dated review record", StringComparison.Ordinal));
            Assert.Contains(recovered, value => value.Contains("Operating close evidence narrative.", StringComparison.Ordinal));
            Assert.Contains(recovered, value => value.Contains("Operating evidence ticket", StringComparison.Ordinal));
            Assert.Contains(recovered, value => value.Contains("Checked independently.", StringComparison.Ordinal));
            Assert.DoesNotContain("Operating close evidence narrative.", Encoding.UTF8.GetString(proof), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
