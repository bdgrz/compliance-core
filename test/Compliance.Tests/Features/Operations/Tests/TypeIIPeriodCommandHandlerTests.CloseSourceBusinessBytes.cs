using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

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
                new Bdgrz.Compliance.Features.Incidents.Readers.ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Host.Content,
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
