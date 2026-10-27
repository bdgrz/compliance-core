using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Readers;

/// <summary>Composes immutable owner deltas and independently materialized deliveries for read consumers.</summary>
public sealed class AuditPackageReader(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
{
    public ValueTask<AuditPackageSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid packageId,
        CancellationToken ct) => ReadAsync(tenantId, programId, packageId, default, null, 0, ct);

    internal async ValueTask<AuditPackageSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid packageId,
        Uuid selectedLogicalId, string? selectedCommandKind, long selectedVersion, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new AuditPackageManifest(tenantId, packageId, selectedLogicalId,
            selectedCommandKind, selectedVersion), ct).ConfigureAwait(false);
        var history = await AuditPackageManifestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (!owner.IsCreated || owner.ProgramId != programId)
            return new AuditPackageSnapshot(owner, null, history);
        var versions = new List<AuditPackageVersionView>();
        AuditPackageView? view = null;
        foreach (var domainEvent in history.Facts)
        {
            var fact = (IAuditPackageManifestFact)domainEvent;
            var versionNumber = checked((int)fact.PackageVersion);
            var index = versionNumber - 1;
            var actor = fact.Operation.Actor;
            var memberId = actor.Kind == "member" && Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var id)
                ? id : (Uuid?)null;
            switch (domainEvent)
            {
                case AuditPackageCreated or AuditPackageAmended:
                    {
                        var value = await PackageContent.ReadAsync<AuditPackageManifestVersionContent>(content, tenantId,
                            fact.Change, ct).ConfigureAwait(false);
                        if (value.ProgramId != programId || value.Manifest.TenantId != tenantId ||
                            value.Manifest.ProgramId != programId || value.Manifest.AuditPackageId != packageId ||
                            value.Manifest.PackageVersion != versionNumber || value.Content.ExaminationId == Uuid.Empty ||
                            value.CreatedBy != actor || value.CreatedAt != fact.OccurredAt ||
                            value.CreatedBy.Kind != "member" || !Uuid.TryParse(value.CreatedBy.Id, CultureInfo.InvariantCulture,
                                out var creator) || creator == Uuid.Empty)
                            throw new ImmutableContentIntegrityException("A manifest version differs from its exact owner.");
                        versions.Add(new AuditPackageVersionView(versionNumber, "draft", value.Content.SourceSnapshotId,
                            value.Content.SourceSnapshotKind, value.Content.SourceSnapshotSha256, value.Content.SelectedContent,
                            value.Manifest, [], [], [], [], value.AmendsVersion, value.AmendmentReason, creator,
                            value.CreatedBy.Display, value.CreatedAt, value.Content.SourcePeriodId));
                        view ??= new AuditPackageView(tenantId, programId, packageId, value.Content.ExaminationId,
                            value.Content.PackageKind, value.Content.Name, "draft", 0, 1, value.CreatedAt,
                            creator, value.CreatedBy.Display, value.CreatedAt, []);
                        break;
                    }
                case AuditPackageBuildRequested:
                    {
                        var intent = await PackageContent.ReadAsync<PackageBuildIntent>(content, tenantId, fact.Change, ct)
                            .ConfigureAwait(false);
                        var attempt = new AuditPackageBuildAttemptView(intent.BuildId, intent.BuildKey, "requested", 0,
                            versions[index].Manifest.Entries.Count, intent.Header.AcceptedAt, intent.Header.AcceptedAt,
                            memberId, actor.Display, null, null, null, null, null, null);
                        versions[index] = versions[index] with
                        {
                            Status = "building",
                            BuildAttempts = [.. versions[index].BuildAttempts, attempt]
                        };
                        break;
                    }
                case AuditPackageBuilt:
                    {
                        var change = await PackageContent.ReadAsync<AuditPackageBuiltChange>(content, tenantId,
                            fact.Change, ct).ConfigureAwait(false);
                        var attempts = versions[index].BuildAttempts.ToArray();
                        var attemptIndex = Array.FindIndex(attempts, attempt => attempt.BuildAttemptId == change.BuildId);
                        if (attemptIndex < 0)
                            throw new ImmutableContentIntegrityException("A successful build has no matching admission.");
                        attempts[attemptIndex] = attempts[attemptIndex] with
                        {
                            Status = "built",
                            CompletedEntryCount = versions[index].Manifest.Entries.Count,
                            UpdatedAt = change.CompletedAt,
                            CompletedAt = change.CompletedAt,
                            ArchiveArtifactId = change.Output.ArchiveArtifactId,
                            ArchiveSha256 = change.Output.ArchiveSha256,
                            ArchiveBytes = change.Output.ArchiveBytes,
                            ResultFingerprint = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(change.Output,
                                    ComplianceCoreJsonContext.Default.AuditPackageBuildOutput)))
                        };
                        versions[index] = versions[index] with { Status = "built", BuildAttempts = attempts };
                        break;
                    }
                case AuditPackageValidated:
                    {
                        var validation = await PackageContent.ReadAsync<AuditPackageValidationView>(content, tenantId,
                            fact.Change, ct).ConfigureAwait(false);
                        if (versions[index].Validations.All(item => item.AuditPackageValidationId != validation.AuditPackageValidationId))
                            versions[index] = versions[index] with
                            {
                                Status = validation.Status == "blocked" ? "validation_blocked" : "validated",
                                Validations = [.. versions[index].Validations, validation]
                            };
                        break;
                    }
                case AuditPackageValidationOverrideApproved:
                    {
                        var change = await PackageContent.ReadAsync<AuditPackageValidationOverrideChange>(content,
                            tenantId, fact.Change, ct).ConfigureAwait(false);
                        var validations = versions[index].Validations.ToArray();
                        var validationIndex = Array.FindIndex(validations,
                            validation => validation.AuditPackageValidationId == change.ValidationId);
                        if (validationIndex < 0)
                            throw new ImmutableContentIntegrityException("An override has no matching validation.");
                        if (validations[validationIndex].Overrides.Any(item => item.FindingId == change.Override.FindingId))
                            break;
                        var overrides = validations[validationIndex].Overrides.Append(change.Override).ToArray();
                        var complete = validations[validationIndex].Findings.Where(finding => finding.BlocksDelivery)
                            .All(finding => overrides.Any(approved => approved.FindingId == finding.FindingId));
                        validations[validationIndex] = validations[validationIndex] with
                        {
                            Overrides = overrides,
                            Status = complete ? "passed_with_overrides" : "blocked",
                            Revision = validations[validationIndex].Revision + 1,
                            UpdatedAt = change.Override.ApprovedAt
                        };
                        versions[index] = versions[index] with
                        {
                            Validations = validations,
                            Status = complete ? "validated_with_overrides" : "validation_blocked"
                        };
                        break;
                    }
                case AuditPackageDisclosureApproved:
                    {
                        var approval = await PackageContent.ReadAsync<AuditPackageDisclosureApprovalView>(content,
                            tenantId, fact.Change, ct).ConfigureAwait(false);
                        if (versions[index].DisclosureApprovals.All(item => item.DisclosureApprovalId != approval.DisclosureApprovalId))
                            versions[index] = versions[index] with
                            {
                                Status = "disclosure_approved",
                                DisclosureApprovals = [.. versions[index].DisclosureApprovals, approval]
                            };
                        break;
                    }
                case AuditPackageDeliveryAdmitted admitted:
                    {
                        versions[index] = versions[index] with { Status = versions[index].Deliveries.Count > 0 ? "delivered" : "delivery_pending" };
                        var delivery = await reader.HydrateAsync(new AuditPackageDelivery(tenantId, packageId,
                            fact.PackageVersion, admitted.Operation.LogicalId), ct).ConfigureAwait(false);
                        if (delivery.Delivered is { } recorded)
                        {
                            var frozen = await PackageContent.ReadAsync<PackageDeliveryRecordingIntent>(content, tenantId,
                                recorded.Change, ct).ConfigureAwait(false);
                            if (recorded.ProgramId != programId || recorded.Change != admitted.Intent ||
                                recorded.Admission != NativeFactReference.Of(owner.Stream, admitted))
                                throw new ImmutableContentIntegrityException("A delivery differs from its exact admission.");
                            var item = new AuditPackageDeliveryView(tenantId, programId, frozen.DeliveryId, packageId,
                                versionNumber, frozen.ManifestId, frozen.ManifestSha256, frozen.ArchiveArtifactId,
                                frozen.ArchiveSha256, frozen.Recipient, frozen.DeliveryReference, "delivered", 1,
                                frozen.DeliveredAt, frozen.RecorderMemberId, frozen.Header.OriginalActor.Display);
                            versions[index] = versions[index] with
                            {
                                Status = "delivered",
                                Deliveries = [.. versions[index].Deliveries, item]
                            };
                        }
                        break;
                    }
            }
            if (view is not null)
                view = view with
                {
                    Revision = fact.Revision,
                    CurrentVersion = versions.Count,
                    Status = versions[^1].Status,
                    Versions = versions.ToArray(),
                    UpdatedAt = fact.OccurredAt
                };
        }
        return new AuditPackageSnapshot(owner, view, history);
    }
}
