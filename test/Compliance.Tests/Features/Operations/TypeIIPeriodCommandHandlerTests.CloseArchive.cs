using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Fact]
    public async Task ShouldArchiveExactPublishedNativeCloseSourceGivenReconciliationApprovalAndFreeze()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCoveredCloseReadyPeriodAsync();
        var root = Path.Combine(Path.GetTempPath(), "bdgrz-type-ii-close-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(root, new byte[32], ArtifactContentLimits.MaxContentLength), fixture.CloseClock);
            var publisher = new TypeIIPeriodCloseSourcePublication(artifacts, fixture.Host.Content);
            var sources = new PublishingCloseSources(publisher);
            var request = await CloseRequestAsync(fixture);
            var validator = Uuid.CreateVersion4();
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", validator.ToString()),
                new Claim("name", "Independent close validator")], "BdgrzSession"));
            var context = new Cntryl.Portia.RequestContext<RecordTypeIIPeriodCloseValidation>(request,
                new RequestDispatchContext(actor, new HttpInvocation("PUT", "/close-validations", "/close-validations", "close-source-test")));
            var validated = await CloseHandler(fixture, sources).HandleAsync(context, CancellationToken.None);
            Assert.True(validated.IsSuccess, validated.Error?.Message);
            var current = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId));
            var approved = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso).HandleAsync(fixture.Context(
                new ApproveTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId, fixture.PeriodId, current.Revision,
                    "Reviewed exact published close source.", [])), CancellationToken.None);
            Assert.True(approved.IsSuccess, approved.Error?.Message);
            current = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId));
            var frozen = await fixture.FreezeHandler(RbacPermissions.ManagementExecutiveCiso).HandleAsync(fixture.Context(
                new FreezeTypeIIPeriodClose(fixture.TenantId, fixture.ProgramId, fixture.PeriodId, Uuid.CreateVersion4(), current.Revision)), CancellationToken.None);
            Assert.True(frozen.IsSuccess, frozen.Error?.Message);
            current = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId));
            var snapshot = Assert.Single(current.Current!.CloseSnapshots);
            var reference = Assert.Single(snapshot.SourceReferences);
            var manifest = await publisher.ReadAsync(current.Current, snapshot.SourceContentManifest!, CancellationToken.None);
            var physical = Assert.Single(manifest.Sources).Content;
            var selected = new AuditPackageSelectionEntry("period", "native_history", reference.SourceType, reference.RecordId,
                reference.Version, physical.FileExtension, reference.ContentSha256, physical.Bytes, "product_calculated", reference.VersionId);
            var input = new AuditPackageContent(fixture.ExaminationId, "type_ii", "type_ii_close_snapshot", snapshot.CloseSnapshotId,
                snapshot.ContentSha256, "Verified period close", [selected], fixture.PeriodId);
            var resolved = await new AuditPackageSourceBuilder(fixture.Reader, fixture.Host.Content).ResolveAsync(fixture.TenantId, fixture.ProgramId,
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
            Assert.Equal(reference.ContentSha256, Bdgrz.Compliance.Features.Kernel.ContentDigest.Sha256(sourceBytes));
            Assert.True(sourceBytes.AsSpan(0, prefix.Length).SequenceEqual(prefix));
            Assert.EndsWith(".snapshot", entry.FullName, StringComparison.Ordinal);
            Assert.Equal("application/vnd.bdgrz.canonical-snapshot-source.v1", physical.MediaType);
            Assert.Equal("type_ii_native_history", body.RootElement.GetProperty("family").GetString());
            Assert.Equal(fixture.PeriodEnd.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                body.RootElement.GetProperty("effective_through").GetString());
            Assert.Equal(manifest.RecordedThrough, body.RootElement.GetProperty("recorded_through").GetDateTimeOffset());
            Assert.Equal(request.CommandId, manifest.CloseOperationId);
            Assert.True(body.RootElement.GetProperty("records").GetArrayLength() > 0);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    sealed class PublishingCloseSources(TypeIIPeriodCloseSourcePublication publication) : ITypeIIPeriodCloseReconciliation
    {
        public async ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriod period, ClaimsPrincipal actor,
            Uuid requesterUserId, OperationProvenance operation, CancellationToken ct)
        {
            var current = period.Current!;
            var facts = period.OccurrenceGenerations.Select(fact => TypeIIPeriodCloseHistory.Capture(period.Stream, fact)).ToArray();
            var published = await publication.PublishAsync(current, operation, "type_ii_native_history", facts, [], ct);
            var manifest = await publication.SealAsync(current, operation, [published.Published], ct,
                TypeIBaselineSourceFamilies.Required.Select(family => TypeIBaselineContentIdentity.Coverage(family, [])).ToArray());
            var expected = current.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            return Result<TypeIIPeriodCloseValidationContent>.Success(new([published.Reference], expected, expected, [], [], [])
            { SourceContentManifest = manifest, RecordedThrough = operation.AcceptedAt });
        }
    }
}
