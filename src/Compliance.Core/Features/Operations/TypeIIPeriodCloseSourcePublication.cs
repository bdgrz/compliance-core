using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Physically publishes exact canonical close sources and seals their complete scope before admission.</summary>
public sealed class TypeIIPeriodCloseSourcePublication(IArtifactContentStore artifacts, IImmutableContentStore content)
{
    const string Domain = "bdgrz.snapshot.source.type_ii_close_native_history.v1\0";

    public async Task<(ExaminationContentReference Reference, SnapshotSourcePublicationEntry Published)> PublishAsync(
        TypeIIPeriodView period, OperationProvenance operation, string family, IReadOnlyList<TypeIIPeriodCloseNativeSource> records,
        IReadOnlyList<TypeIIPeriodCloseOccurrenceMatch> matches, CancellationToken ct)
    {
        var header = new DurableIntentHeader(operation.RootOperationId, operation.Owner, operation.AffectedOwner,
            operation.ExpectedRevisionOwner, operation.Actor, operation.Episode, operation.OriginalExpectedRevision,
            operation.AuthoredIntentSha256, operation.AcceptedAt);
        var value = new TypeIIPeriodCloseSourceEnvelope(header, period.PeriodStart, period.PeriodEnd,
            operation.AcceptedAt, family, records, matches);
        var json = JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.TypeIIPeriodCloseSourceEnvelope);
        var prefix = Encoding.UTF8.GetBytes(Domain);
        if ((long)json.Length + prefix.Length > ArtifactContentLimits.MaxContentLength)
            throw new ImmutableContentIntegrityException("The complete close source exceeds supported artifact storage size.");
        var bytes = new byte[prefix.Length + json.Length];
        prefix.CopyTo(bytes, 0);
        json.CopyTo(bytes, prefix.Length);
        var digest = ContentDigest.Sha256(bytes);
        var blob = new SnapshotSourceBlob(digest, bytes.Length, "application/vnd.bdgrz.canonical-snapshot-source.v1",
            "snapshot", "Canonical Type II close source proof bytes (domain, NUL delimiter, exact complete JSON payload).");
        using var capture = SnapshotSourceCapture.Begin(period.TenantId);
        SnapshotSourceCapture.Material? material = null;
        var bodies = records.SelectMany(record => record.PhysicalContentBlobs ?? []).ToArray();
        if (bodies.Length > 0)
        {
            _ = SnapshotSourceCapture.HashData(bytes);
            SnapshotSourceCapture.TypeIIClose(digest, bytes, bodies);
            if (!capture.TryGet(digest, out material) || material is null)
                throw new ImmutableContentIntegrityException("The complete selected operating source bytes were not captured.");
            blob = material.Blob;
        }
        if (blob.Bytes > ArtifactContentLimits.MaxContentLength)
            throw new ImmutableContentIntegrityException("The complete close source exceeds supported artifact storage size.");
        await using var stream = material is null ? (Stream)new MemoryStream(bytes, writable: false)
            : new FileStream(material.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        var written = await artifacts.StoreIfAbsentAsync(period.TenantId, stream, ct).ConfigureAwait(false);
        if (written.Content.TenantId != period.TenantId || written.Content.Sha256 != blob.Sha256 || written.Content.Length != blob.Bytes)
            throw new ImmutableContentIntegrityException("The complete close source differs from its exact published byte identity.");
        await using var verified = await artifacts.OpenVerifiedAsync(written.Content, ct).ConfigureAwait(false);
        if (verified is null)
            throw new ImmutableContentUnavailableException("The published close source is not available for verified read yet.");
        var version = Uuid.CreateVersion5(period.PeriodId, $"{family}:{digest}");
        var reference = new ExaminationContentReference(family, period.PeriodId, version, period.Revision, digest);
        return (reference, new SnapshotSourcePublicationEntry(family, period.PeriodId, version, period.Revision, digest, blob));
    }

    public async Task<SnapshotSourcePublicationManifest> ReadAsync(TypeIIPeriodView period,
        ImmutableContentRef reference, CancellationToken ct) =>
        await SnapshotSourcePublicationReader.ReadAsync(content, period.TenantId, period.ProgramId, period.ExaminationId,
            reference, ct).ConfigureAwait(false);

    public Task<ImmutableContentRef> SealAsync(TypeIIPeriodView period, OperationProvenance operation,
        IReadOnlyList<SnapshotSourcePublicationEntry> sources, CancellationToken ct,
        IReadOnlyList<TypeIBaselineSourceCoverage>? coverage = null) => content.WriteJsonAsync(period.TenantId.ToString(),
        Uuid.CreateVersion4(), SnapshotSourcePublicationManifest.Schema, new SnapshotSourcePublicationManifest(period.TenantId,
            period.ProgramId, period.ExaminationId, sources)
        {
            ClosePeriodId = period.PeriodId,
            CloseOperationId = operation.RootOperationId,
            RecordedThrough = operation.AcceptedAt,
            SourceCoverage = coverage,
        }, ct);
}
