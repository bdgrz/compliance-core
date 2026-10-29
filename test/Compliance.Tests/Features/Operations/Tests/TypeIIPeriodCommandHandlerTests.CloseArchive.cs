using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Changes.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
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
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Fact]
    public Task ShouldArchiveExactPublishedNativeCloseSourceGivenReconciliationApprovalAndFreeze() => ArchiveAsync(includeChange: false);

    [Fact]
    public Task ShouldArchiveExactVerifiedChangeBodiesGivenNativeCloseHistoryWithGovernedReferences() => ArchiveAsync(includeChange: true);

    [Fact]
    public Task ShouldArchiveExactVerifiedIncidentComponentsGivenNativeCloseHistoryWithGovernedReferences() => ArchiveAsync(includeChange: false, includeIncident: true);

    static async Task ArchiveAsync(bool includeChange, bool includeIncident = false)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCoveredCloseReadyPeriodAsync();
        SignificantChangeRules? change = null;
        ComplianceIncidentRules? incident = null;
        if (includeChange)
        {
            change = new SignificantChangeRules(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4());
            Assert.Null(change.Record(new SignificantChangeContent(null, null, null, "Exact source-byte history", "A governed change body in the close archive.",
                fixture.PeriodStart, null, fixture.ProposerMemberId, Enumerable.Range(0, 1000).Select(index => Uuid.CreateVersion5(change.Id, "system:" + index)).ToArray(),
                "high", "confidential", []), fixture.ProposerMemberId, "Recorder", StartedAt));
            Assert.Null(change.Assess(1, new SignificantChangeImpactContent("not_significant", "No key boundary or control changes.",
                false, false, false, false, ["other"], [], [], [], [], [], false), fixture.ActorMemberId, "Assessor", StartedAt.AddHours(1)));
            await ChangeSeeds.WriteAsync(fixture.Writer, fixture.Host.Content, change);
        }
        if (includeIncident)
        {
            incident = new ComplianceIncidentRules(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4());
            Assert.Null(incident.Record(new ComplianceIncidentContent(null, null, null, null, "Exact incident source-byte history", "Complete governed incident narrative.",
                "operational", "medium", StartedAt, StartedAt.AddMinutes(10), fixture.ProposerMemberId, false, false,
                Enumerable.Range(0, 1000).Select(index => Uuid.CreateVersion5(incident.Id, "system:" + index)).ToArray(), "confidential",
                Enumerable.Range(0, 1000).Select(index => Uuid.CreateVersion5(incident.Id, "evidence:" + index)).ToArray()),
                fixture.ProposerMemberId, "Recorder", StartedAt.AddMinutes(20)));
            Assert.Null(incident.Assess(1, new ComplianceIncidentImpactAssessmentContent("not_significant", "Reviewed all operating impacts.", true, true, true, true,
                ["scope", "control", "risk", "vendor", "evidence"], [], [], [], false, false, false), fixture.ActorMemberId, "Assessor", StartedAt.AddMinutes(30)));
            Assert.Null(incident.Resolve(2, new VerifiedIncidentResolution("Incident contained and reviewed.", StartedAt.AddMinutes(45), [Uuid.CreateVersion5(incident.Id, "evidence:0")], []),
                fixture.ActorMemberId, "Resolver", StartedAt.AddHours(1)));
            await IncidentSeeds.WriteAsync(fixture.Writer, fixture.Host.Content, incident);
        }
        var root = Path.Combine(Path.GetTempPath(), "bdgrz-type-ii-close-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(root, new byte[32], ArtifactContentLimits.MaxContentLength), fixture.CloseClock);
            var publisher = new TypeIIPeriodCloseSourcePublication(artifacts, fixture.Host.Content);
            var sources = new PublishingCloseSources(publisher, includeChange || includeIncident ? new TypeIIPeriodCloseHistory(fixture.Events,
                new RestrictedComplianceRecordVisibility(new RecordingPermissionAuthorizer(true)),
                new SignificantChangeReader(fixture.Reader, fixture.Events, fixture.Host.Content, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Content, new ComplianceIncidentReader(fixture.Reader, fixture.Events, fixture.Host.Content, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()), fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()) : null);
            var request = await CloseRequestAsync(fixture);
            var validator = Uuid.CreateVersion4();
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", validator.ToString()),
                new Claim("name", "Independent close validator")], "BdgrzSession"));
            var context = new Cntryl.Portia.RequestContext<RecordTypeIIPeriodCloseValidation>(request,
                new RequestDispatchContext(actor, new HttpInvocation("PUT", "/close-validations", "/close-validations", "close-source-test")));
            var validated = await CloseHandler(fixture, sources).HandleAsync(context, CancellationToken.None);
            Assert.True(validated.IsSuccess, validated.Error?.Message);
            var current = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
            var approved = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso).HandleAsync(fixture.Context(
                new ApproveTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PeriodId, current.Revision,
                    "Reviewed exact published close source.", [])), CancellationToken.None);
            Assert.True(approved.IsSuccess, approved.Error?.Message);
            current = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
            var frozen = await fixture.FreezeHandler(RbacPermissions.ManagementExecutiveCiso).HandleAsync(fixture.Context(
                new FreezeTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PeriodId, Uuid.CreateVersion4(), current.Revision)), CancellationToken.None);
            Assert.True(frozen.IsSuccess, frozen.Error?.Message);
            current = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
            var snapshot = Assert.Single(current.View!.CloseSnapshots);
            var reference = Assert.Single(snapshot.SourceReferences);
            var manifest = await publisher.ReadAsync(current.View, snapshot.SourceContentManifest!, CancellationToken.None);
            var physical = Assert.Single(manifest.Sources).Content;
            var selected = new AuditPackageSelectionEntry("period", "native_history", reference.SourceType, reference.RecordId,
                reference.Version, physical.FileExtension, reference.ContentSha256, physical.Bytes, "product_calculated", reference.VersionId);
            var input = new AuditPackageContent(fixture.ExaminationId, "type_ii", "type_ii_close_snapshot", snapshot.CloseSnapshotId,
                snapshot.ContentSha256, "Verified period close", [selected], fixture.PeriodId);
            var resolved = await new AuditPackageSourceBuilder(new ExaminationEngagementReader(fixture.Reader, fixture.Events, fixture.Host.Content), fixture.Periods, fixture.Host.Content).ResolveAsync(fixture.TenantId, fixture.ProgramId,
                input, CancellationToken.None);
            Assert.True(resolved.IsSuccess, resolved.Error?.Message);
            var rules = new AuditPackageRules(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4());
            Assert.Null(rules.Create(resolved.Value!, fixture.ActorMemberId, "Package recorder", fixture.CloseClock.GetUtcNow()));

            // Act
            var built = await new AuditPackageArchiveBuilder(artifacts).BuildAsync(fixture.TenantId, rules.Current!.Versions[^1].Manifest, CancellationToken.None);
            Assert.True(built.IsSuccess, built.Error?.Message);
            await using var stored = await artifacts.OpenVerifiedAsync(new ArtifactContentReference(fixture.TenantId, built.Value.ArchiveSha256!,
                built.Value.ArchiveBytes!.Value));
            using var zip = new ZipArchive(stored!, ZipArchiveMode.Read);
            var entry = Assert.Single(zip.Entries);
            await using var payload = entry.Open();
            using var bytes = new MemoryStream();
            await payload.CopyToAsync(bytes);
            var sourceBytes = bytes.ToArray();
            var prefix = Encoding.UTF8.GetBytes("bdgrz.snapshot.source.type_ii_close_native_history.v1\0");
            using var body = JsonDocument.Parse(sourceBytes.AsMemory(prefix.Length));

            // Assert
            Assert.Equal(physical.Bytes, sourceBytes.Length);
            Assert.Equal(reference.ContentSha256, Bdgrz.Compliance.Features.Kernel.Services.ContentDigest.Sha256(sourceBytes));
            Assert.True(sourceBytes.AsSpan(0, prefix.Length).SequenceEqual(prefix));
            Assert.EndsWith(".snapshot", entry.FullName, StringComparison.Ordinal);
            Assert.Equal("application/vnd.bdgrz.canonical-snapshot-source.v1", physical.MediaType);
            Assert.Equal("type_ii_native_history", body.RootElement.GetProperty("family").GetString());
            Assert.Equal(fixture.PeriodEnd.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                body.RootElement.GetProperty("effective_through").GetString());
            Assert.Equal(manifest.RecordedThrough, body.RootElement.GetProperty("recorded_through").GetDateTimeOffset());
            Assert.Equal(request.CommandId, manifest.CloseOperationId);
            Assert.True(body.RootElement.GetProperty("records").GetArrayLength() > 0);
            if (includeIncident)
            {
                var recorded = body.RootElement.GetProperty("records").EnumerateArray().Single(item =>
                    item.GetProperty("payload").TryGetProperty("header", out var header) &&
                    header.TryGetProperty("incident_id", out var incidentId) && incidentId.GetString() == incident!.Id.ToString() &&
                    header.GetProperty("revision").GetInt64() == 1);
                var blob = recorded.GetProperty("content_blobs").EnumerateArray().Single(item =>
                    item.GetProperty("reference").GetProperty("schema").GetString() == ComplianceIncidentContentStore.VersionSchema);
                var exact = Convert.FromBase64String(blob.GetProperty("base64").GetString()!);
                Assert.Equal(blob.GetProperty("reference").GetProperty("bytes").GetInt64(), exact.LongLength);
                Assert.Equal(blob.GetProperty("reference").GetProperty("sha256").GetString(), Bdgrz.Compliance.Features.Kernel.Services.ContentDigest.Sha256(exact));
                var reconstructed = JsonSerializer.Deserialize(exact, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ComplianceIncidentVersionView);
                Assert.Equal("Exact incident source-byte history", reconstructed!.Content.Title);
                Assert.Equal(1000, reconstructed.Content.AffectedSystemIds.Count);
                Assert.Equal(1000, reconstructed.Content.EvidenceArtifactIds.Count);
                Assert.Equal(incident!.Current!.Current!.ContentSha256, reconstructed.ContentSha256);
                Assert.Equal(3, body.RootElement.GetProperty("records").EnumerateArray().Count(item => item.GetProperty("payload").TryGetProperty("header", out var header) &&
                    header.TryGetProperty("incident_id", out var id) && id.GetString() == incident.Id.ToString()));
            }
            if (includeChange)
            {
                var recorded = body.RootElement.GetProperty("records").EnumerateArray().Single(item =>
                    item.GetProperty("payload").TryGetProperty("header", out var header) &&
                    header.TryGetProperty("significant_change_id", out var changeId) && changeId.GetString() == change!.Id.ToString() &&
                    header.GetProperty("revision").GetInt64() == 1);
                var blob = recorded.GetProperty("content_blobs")[0];
                var exact = Convert.FromBase64String(blob.GetProperty("base64").GetString()!);
                Assert.Equal(blob.GetProperty("reference").GetProperty("bytes").GetInt64(), exact.LongLength);
                Assert.Equal(blob.GetProperty("reference").GetProperty("sha256").GetString(), Bdgrz.Compliance.Features.Kernel.Services.ContentDigest.Sha256(exact));
                var reconstructed = JsonSerializer.Deserialize(exact, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.SignificantChangeView);
                Assert.Equal("Exact source-byte history", reconstructed!.Current!.Content.Title);
                Assert.Equal(1000, reconstructed.Current.Content.AffectedSystemIds.Count);
                Assert.Equal(change!.Current!.Current!.ContentSha256, reconstructed.Current.ContentSha256);
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    sealed class PublishingCloseSources(TypeIIPeriodCloseSourcePublication publication, TypeIIPeriodCloseHistory? history = null) : ITypeIIPeriodCloseReconciliation
    {
        public async ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriodReadSnapshot period, ClaimsPrincipal actor,
            Uuid requesterUserId, OperationProvenance operation, CancellationToken ct)
        {
            var current = period.View!;
            var native = period.OccurrenceGenerations.Select(fact => TypeIIPeriodCloseHistory.Capture(period.Stream, fact)).ToList();
            if (history is not null)
            {
                var captured = await history.ReadAsync(current, actor, operation.AcceptedAt, ct);
                Assert.Empty(captured.Blockers);
                native.AddRange(captured.Records);
            }
            var facts = native.ToArray();
            var published = await publication.PublishAsync(current, operation, "type_ii_native_history", facts, [], ct);
            var manifest = await publication.SealAsync(current, operation, [published.Published], ct,
                TypeIBaselineSourceFamilies.Required.Select(family => TypeIBaselineContentIdentity.Coverage(family, [])).ToArray());
            var expected = current.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            return Result<TypeIIPeriodCloseValidationContent>.Success(new([published.Reference], expected, expected, [], [], [])
            { SourceContentManifest = manifest, RecordedThrough = operation.AcceptedAt });
        }
    }
}
