using System.Security.Cryptography;
using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Readers;
namespace Bdgrz.Compliance.Features.AuditPopulations.Handlers;

public sealed class GenerateAuditPopulationExportHandler(AuditPopulationMutations mutations,
    AuditPopulationReader populations,
    IAggregateReader reader, EvidenceIntake evidenceIntake, TimeProvider clock,
    NativeOperationExecutor operations, IImmutableContentStore content, IDomainEventReader events)
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

        var population = await populations.ReadAsync(request.TenantId,
            request.ProgramId, request.PopulationId, ct).ConfigureAwait(false);
        if (population.View is not { } current)
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
        var appended = await mutations.AppendUnqualifiedAsync(context, request.TenantId, request.ProgramId,
            request.PopulationId, rules =>
            {
                if (rules.Current is not { } latest)
                    return CommandFailure.MissingRecord("The audit population was not found.");
                if (latest.Exports.FirstOrDefault(item => item.ExportId == exportId) is { } raced)
                    return raced.PopulationVersion == version.Version && raced.Format == format &&
                        raced.PopulationContentSha256 == version.ContentSha256 && raced.ArtifactId == artifact.Id &&
                        raced.OutputContentSha256 == outputSha && raced.OutputContentLength == bytes.LongLength
                        ? null : CommandFailure.StateConflict("An export key cannot be reused for different population content.");
                return rules.RecordExport(rules.Revision, exportId, version.Version, format, fileName, artifact.Id,
                    outputSha, bytes.LongLength, actorMemberId, actorDisplay, exportAt);
            }, ct).ConfigureAwait(false);
        if (!appended.IsSuccess)
            return Result<AuditPopulationExportReceipt>.Failure(appended.Error!);
        var recorded = appended.Value.Exports.Single(item => item.ExportId == exportId);
        return Result<AuditPopulationExportReceipt>.Success(new(recorded, artifact.State!, artifact.StateReason));
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
        var commandId = Uuid.CreateVersion5(artifact.Id, "generated-export-retention-basis:v1");
        var request = new RecordArtifactRetentionBasis(commandId, registration.TenantId, "evidence_artifact",
            artifact.Id, source.Value.Source.ContentSha256, 0, RetentionReason, periodStart, periodEnd);
        var recorded = await new ArtifactRetentionMutation(operations, reader, content, clock, events)
            .RecordBasisAsync(context, request, registration.Collector, ct).ConfigureAwait(false);
        if (!recorded.IsSuccess)
            return recorded;
        var current = await reader.HydrateAsync(new ArtifactRetentionBasis(source.Value.Source), ct).ConfigureAwait(false);
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
