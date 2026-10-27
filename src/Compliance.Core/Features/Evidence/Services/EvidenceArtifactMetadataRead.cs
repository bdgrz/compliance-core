using System.Security.Cryptography;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Projections;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Services;

/// <summary>Captures existing canonical metadata without opening, disclosing or copying content.</summary>
sealed class EvidenceArtifactMetadataRead(IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<EvidenceArtifactMetadataView>> GetAsync(Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        var captured = await GetCapturedAsync(tenantId, artifactId, ct).ConfigureAwait(false);
        return captured.IsSuccess ? Result<EvidenceArtifactMetadataView>.Success(captured.Value.Metadata) :
            Result<EvidenceArtifactMetadataView>.Failure(captured.Error);
    }

    public ValueTask<Result<EvidenceArtifactMetadataSnapshot>> GetCapturedAsync(Uuid tenantId, Uuid artifactId, CancellationToken ct) =>
        CaptureAsync(reader, events, tenantId, artifactId, ct);

    internal static async ValueTask<Result<EvidenceArtifactMetadataSnapshot>> CaptureAsync(IAggregateReader reader,
        IDomainEventReader events, Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        EvidenceArtifact artifact;
        try
        {
            artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (IsInvalidEvidenceHistory(exception))
        {
            return Missing();
        }
        if (artifact.Registration is not { } registration || registration.TenantId != tenantId ||
            registration.ArtifactId != artifactId || registration.Metadata.EventId == Uuid.Empty ||
            registration.Metadata.AggregateId != artifactId ||
            registration.Content is not { Title: not null, EvidenceType: not null, Source: not null, HandlingClass: not null } ||
            registration.Collector is null ||
            EvidenceRules.Validate(registration.Content, registration.ContentSha256, registration.ContentLength) is not null)
            return Missing();
        var position = artifact.CommittedStreamPosition;
        ulong expectedOffset = 0;
        string? state = null;
        string? reason = null;
        DateTimeOffset? availabilityRecordedAt = null;
        await foreach (var source in events.ReadAsync(artifact.Stream, 0, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (source.Stream != artifact.Stream || source.ResourceOffset != expectedOffset || source.ResourceOffset >= position)
                return Changed();
            if (source.Event.Metadata.EventId == Uuid.Empty || source.Event.Metadata.AggregateId != artifactId)
                return Missing();
            expectedOffset++;
            switch (source.Event)
            {
                case EvidenceArtifactRegistered ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                        state is null && ev == registration &&
                                                        ev.Metadata.EventId == registration.Metadata.EventId:
                    state = EvidenceArtifactStates.PendingInspection;
                    break;
                case EvidenceArtifactInspected ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                       state == EvidenceArtifactStates.PendingInspection && ValidInspection(ev):
                    state = ev.State;
                    reason = ev.Reason;
                    if (ev.State == EvidenceArtifactStates.Available)
                        availabilityRecordedAt = ev.InspectedAt;
                    break;
                case EvidenceArtifactQuarantineReleased ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                                state == EvidenceArtifactStates.Quarantined && reason == EvidenceArtifactStates.Malware &&
                                                                ev.Revision == (long)expectedOffset && ev.RequestId != Uuid.Empty &&
                                                                ev.RescanState == "clean" && ev.RescannedAt != default &&
                                                                ev.RescannedAt <= ev.DecidedAt && ev.DecidedAt != default &&
                                                                !string.IsNullOrWhiteSpace(ev.Rationale) && ev.Rationale.Length <= EvidenceRules.MaximumTextLength &&
                                                                ev.DecidedBy is { Kind: "member", Id.Length: > 0, Display.Length: > 0 }:
                    state = EvidenceArtifactStates.Available;
                    reason = null;
                    availabilityRecordedAt = ev.DecidedAt;
                    break;
                case EvidenceArtifactDownloadIssued ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                           state == EvidenceArtifactStates.Available && reason is null &&
                                                           ev.Revision == (long)expectedOffset && ev.RequestId != Uuid.Empty &&
                                                           ev.ContentSha256 == registration.ContentSha256 &&
                                                           ev.ContentLength == registration.ContentLength &&
                                                           !string.IsNullOrWhiteSpace(ev.Purpose) &&
                                                           ev.Purpose.Trim() == ev.Purpose &&
                                                           ev.Purpose.Length <= EvidenceRules.MaximumTextLength &&
                                                           ev.IssuedBy is
                                                           {
                                                               Kind: "member", Id.Length: > 0,
                                                               Display.Length: > 0 and <= 200
                                                           } &&
                                                           ev.IssuedAt >= registration.RegisteredAt &&
                                                           ev.IssuedAt != default:
                    break;
                default:
                    return Missing();
            }
        }
        if (expectedOffset != position)
            return Changed();
        if (state != artifact.State || reason != artifact.StateReason)
            return Missing();
        var view = new EvidenceArtifactMetadataView(tenantId, artifactId, position,
            registration.ContentSha256, registration.ContentLength,
            state == EvidenceArtifactStates.Rejected ? null : registration.Content,
            registration.Collector, registration.RegisteredAt, state!, reason);
        var current = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct).ConfigureAwait(false);
        if (current.CommittedStreamPosition != position)
            return Changed();
        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(registration,
            ComplianceCoreJsonContext.Default.EvidenceArtifactRegistered)));
        return Result<EvidenceArtifactMetadataSnapshot>.Success(new(view, registration.Metadata.EventId, digest, availabilityRecordedAt));
    }

    public async ValueTask<Result> CheckAsync(EvidenceArtifactMetadataView original, CancellationToken ct)
    {
        var current = await reader.HydrateAsync(new EvidenceArtifact(original.TenantId, original.ArtifactId), ct).ConfigureAwait(false);
        return current.CommittedStreamPosition == original.SourcePosition ? Result.Success : Result.Failure(
            new RequestError(RequestErrorKind.Conflict, "Artifact metadata changed during this request.", isTransient: true));
    }

    static bool ValidInspection(EvidenceArtifactInspected ev) => ev.State switch
    {
        EvidenceArtifactStates.Available => ev.Reason is null,
        EvidenceArtifactStates.Quarantined => ev.Reason == EvidenceArtifactStates.Malware,
        EvidenceArtifactStates.Rejected => ev.Reason is EvidenceArtifactStates.SecretDetected or EvidenceArtifactStates.Invalid,
        _ => false
    };

    static bool IsInvalidEvidenceHistory(InvalidOperationException exception) => exception.Message is
        "The evidence artifact registration event is invalid." or
        "The evidence artifact inspection event is invalid." or
        "The evidence quarantine release event is invalid.";

    static Result<EvidenceArtifactMetadataSnapshot> Missing() => Result<EvidenceArtifactMetadataSnapshot>.Failure(
        new RequestError(RequestErrorKind.NotFound, "The artifact metadata was not found."));
    static Result<EvidenceArtifactMetadataSnapshot> Changed() => Result<EvidenceArtifactMetadataSnapshot>.Failure(
        new RequestError(RequestErrorKind.Conflict, "Artifact metadata changed during this request.", isTransient: true));
}
