using System.Security.Cryptography;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class GenerateAuditPopulationExportHandler(IAggregateExecutor executor,
    IAggregateReader reader, EvidenceIntake evidenceIntake, TimeProvider clock)
    : IRequestHandler<GenerateAuditPopulationExport, AuditPopulationExportReceipt>
{
    const string RetentionReason = "M0-D16 default retention for a generated audit population export.";

    public async ValueTask<Result<AuditPopulationExportReceipt>> HandleAsync(
        IRequestContext<GenerateAuditPopulationExport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Failure(RequestErrorKind.Forbidden,
                "Generating an audit population export requires a personal HTTP actor.");

        var format = request.Format?.Trim().ToLowerInvariant();
        var exportKey = request.ExportKey?.Trim();
        if (format is not ("csv" or "json") || string.IsNullOrWhiteSpace(exportKey) || exportKey.Length > 200 ||
            request.Version is < 1 or > int.MaxValue)
            return Failure(RequestErrorKind.Validation,
                "An export requires a supported format, bounded stable key, and positive population version.");

        var population = await reader.HydrateAsync(new AuditPopulation(request.TenantId,
            request.ProgramId, request.PopulationId), ct).ConfigureAwait(false);
        if (population.Current is not { } current)
            return Failure(RequestErrorKind.NotFound, "The audit population was not found.");

        var exportId = Uuid.CreateVersion5(request.PopulationId, "audit-population-export:" + exportKey);
        if (current.Exports.FirstOrDefault(item => item.ExportId == exportId) is { } existing)
        {
            if (existing.PopulationVersion != (int)request.Version || existing.Format != format)
                return Failure(RequestErrorKind.Conflict,
                    "An export key cannot be reused for a different population version or format.");
            return await ExistingReceiptAsync(request.TenantId, existing, ct).ConfigureAwait(false);
        }

        if (current.Status is not ("frozen" or "amending" or "reconciled") || current.Versions.Count == 0)
            return Failure(RequestErrorKind.Conflict,
                "Population exports require an existing immutable frozen version.");
        var version = current.Versions.FirstOrDefault(item => item.Version == (int)request.Version);
        if (version is null)
            return Failure(RequestErrorKind.NotFound, "The frozen population version was not found.");
        if (current.DefinitionVersions.FirstOrDefault(item => item.Version == version.DefinitionVersion) is not { } definition)
            return Failure(RequestErrorKind.Conflict,
                "The frozen population version has no matching definition snapshot.");

        var bytes = AuditPopulationExportDocument.Build(current, version, format);
        var outputSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var fileName = $"audit-population-{request.PopulationId}-v{version.Version}.{format}";
        var capturedAt = clock.GetUtcNow();
        var metadata = new EvidenceArtifactContent($"Audit population v{version.Version} export",
            $"Deterministic {format.ToUpperInvariant()} export of frozen population {request.PopulationId}, version {version.Version}.",
            "audit_population_export", "Bdgrz audit population", capturedAt,
            definition.Content.PeriodStart, definition.Content.PeriodEnd, "confidential");
        var collector = ActorReference.ForMember(actorMemberId, actorDisplay);
        var capture = await evidenceIntake.CaptureAsync(request.TenantId,
            new MemoryStream(bytes, writable: false), metadata, collector,
            new RequestDispatchContext(context.Actor, context.Invocation), ct).ConfigureAwait(false);
        if (!capture.IsSuccess)
            return Result<AuditPopulationExportReceipt>.Failure(capture.Error);
        if (capture.Value.State == EvidenceArtifactStates.Rejected)
            return Failure(RequestErrorKind.Conflict,
                "The generated export failed evidence inspection and was rejected.");

        var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
            capture.Value.ArtifactId), ct).ConfigureAwait(false);
        if (!MatchesGeneratedArtifact(artifact, metadata, outputSha, bytes.LongLength) ||
            artifact.State is not (EvidenceArtifactStates.PendingInspection or EvidenceArtifactStates.Available or
                EvidenceArtifactStates.Quarantined))
            return Failure(RequestErrorKind.Conflict,
                "The generated export does not match its canonical evidence registration.");

        var retained = await EnsureRetentionAsync(context, artifact, definition.Content.PeriodStart,
            definition.Content.PeriodEnd, ct).ConfigureAwait(false);
        if (!retained.IsSuccess)
            return Result<AuditPopulationExportReceipt>.Failure(retained.Error);

        var exportAt = clock.GetUtcNow();
        var export = new AuditPopulationExportView(exportId, version.Version, format, fileName,
            version.ContentSha256, version.CloseSnapshotId, exportAt, actorMemberId, actorDisplay,
            artifact.Id, outputSha, bytes.LongLength);
        var receipt = new AuditPopulationExportReceipt(export, artifact.State!, artifact.StateReason);
        return await executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
            request.PopulationId), aggregate =>
        {
            if (aggregate.Current is not { } latest)
                return AggregateOutcome.Discard(Result<AuditPopulationExportReceipt>.Failure(
                    new RequestError(RequestErrorKind.NotFound, "The audit population was not found.")));
            if (latest.Exports.FirstOrDefault(item => item.ExportId == exportId) is { } raced)
            {
                if (raced.PopulationVersion == version.Version && raced.Format == format &&
                    raced.PopulationContentSha256 == version.ContentSha256 &&
                    raced.ArtifactId == artifact.Id && raced.OutputContentSha256 == outputSha &&
                    raced.OutputContentLength == bytes.LongLength)
                    return AggregateOutcome.Discard(Result<AuditPopulationExportReceipt>.Success(
                        new AuditPopulationExportReceipt(raced, artifact.State!, artifact.StateReason)));
                return AggregateOutcome.Discard(Result<AuditPopulationExportReceipt>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "An export key cannot be reused for different population content.")));
            }
            var failure = aggregate.RecordExport(aggregate.Revision, exportId, version.Version, format,
                fileName, artifact.Id, outputSha, bytes.LongLength, actorMemberId, actorDisplay, exportAt);
            return CommandFailureRequestAdapter.ToOutcome(failure, receipt);
        }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<AuditPopulationExportReceipt>> ExistingReceiptAsync(Uuid tenantId,
        AuditPopulationExportView export, CancellationToken ct)
    {
        if (export.ArtifactId == Uuid.Empty || export.OutputContentSha256 is not { } sha ||
            export.OutputContentLength is not { } length)
            return Failure(RequestErrorKind.Conflict,
                "This legacy export record has no governed evidence artifact and must be rebuilt.");
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, export.ArtifactId), ct)
            .ConfigureAwait(false);
        if (artifact.ContentSha256 != sha || artifact.ContentLength != length || artifact.State is null)
            return Failure(RequestErrorKind.Conflict,
                "The governed export artifact no longer matches its recorded identity.");
        return Result<AuditPopulationExportReceipt>.Success(new(export, artifact.State, artifact.StateReason));
    }

    async ValueTask<Result> EnsureRetentionAsync<TRequest>(IRequestContext<TRequest> context,
        EvidenceArtifact artifact, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        if (artifact.Registration is not { } registration || registration.Content.PeriodStart != periodStart ||
            registration.Content.PeriodEnd != periodEnd)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The export artifact retention period differs from its frozen population definition."));
        var source = await ArtifactRetentionSourceReader.LoadAsync(reader, registration.TenantId,
            "evidence_artifact", artifact.Id, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        var recorded = await executor.ExecuteAsync(new ArtifactRetention(registration.TenantId,
            "evidence_artifact", artifact.Id), aggregate => AggregateOutcome.CommitOnSuccess(
            aggregate.RecordBasis(source.Value, aggregate.Revision, periodStart, periodEnd,
                RetentionReason, registration.Collector, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
        if (!recorded.IsSuccess)
            return recorded;
        var current = await reader.HydrateAsync(new ArtifactRetention(registration.TenantId,
            "evidence_artifact", artifact.Id), ct).ConfigureAwait(false);
        if (current.Basis is not { } basis || basis.PeriodStart != periodStart || basis.PeriodEnd != periodEnd ||
            basis.RetainsThrough != periodEnd.AddYears(7) || basis.Reason != RetentionReason ||
            basis.RecordedBy != registration.Collector)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The generated export does not have its exact M0-D16 retention basis."));
        return Result.Success;
    }

    static bool MatchesGeneratedArtifact(EvidenceArtifact artifact, EvidenceArtifactContent expected,
        string sha, long length) => artifact.Registration is { } registration &&
        registration.Content.Title == expected.Title && registration.Content.Description == expected.Description &&
        registration.Content.EvidenceType == expected.EvidenceType && registration.Content.Source == expected.Source &&
        registration.Content.PeriodStart == expected.PeriodStart && registration.Content.PeriodEnd == expected.PeriodEnd &&
        registration.Content.HandlingClass == expected.HandlingClass && registration.ContentSha256 == sha &&
        registration.ContentLength == length;

    static Result<AuditPopulationExportReceipt> Failure(RequestErrorKind kind, string message) =>
        Result<AuditPopulationExportReceipt>.Failure(new RequestError(kind, message));
}
