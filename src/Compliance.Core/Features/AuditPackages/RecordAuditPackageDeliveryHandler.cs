using System.Globalization;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Admits a personal historical handoff and freezes its version before the child is materialized.</summary>
public sealed class RecordAuditPackageDeliveryHandler(AuditPackageRootOperation operations,
    IImmutableContentStore content, IArtifactContentStore artifacts)
    : IRequestHandler<RecordAuditPackageDelivery, OperationAcceptedResponse>
{
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordAuditPackageDelivery> context, CancellationToken ct)
    {
        var request = context.Request;
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            request.DeliveryId, nameof(RecordAuditPackageDelivery), request.ExpectedManifestRevision,
            _ => request.PackageVersion,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedManifestRevision, request.PackageVersion,
                allowFrozen: true), async (snapshot, operation, token) =>
            {
                if (request.DeliveredAt == default || request.DeliveredAt > operation.AcceptedAt ||
                    string.IsNullOrWhiteSpace(request.Recipient) || string.IsNullOrWhiteSpace(request.DeliveryReference) ||
                    request.DeliveryId != AuditPackageCommandIdentity.Delivery(request.PackageId, request.PackageVersion,
                        request.Recipient, request.DeliveryReference))
                    return PackageChanges.Invalid("Delivery requires its stable identity and an authored handoff time in the past.");
                var view = snapshot.View!;
                var version = view.Versions[^1];
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId, view);
                if (rules.RecordDelivery(request.ExpectedManifestRevision, checked((int)request.PackageVersion),
                        request.Recipient, request.DeliveryReference, PackageChanges.Member(operation), operation.Actor.Display,
                        operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                var build = version.BuildAttempts.Last(attempt => attempt.Status == "built");
                var unavailable = await AuditPackageArchiveVerifier.FailureCodeAsync(artifacts, request.TenantId,
                    build, version.Manifest, token).ConfigureAwait(false);
                if (unavailable is not null)
                    return Result<PreparedPackageDelta>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact actual archive must verify before delivery admission: " + unavailable,
                        unavailable == "archive_content_unavailable"));
                var approval = version.DisclosureApprovals.Last(item => item.Recipient == request.Recipient.Trim() &&
                    item.ManifestId == version.Manifest.AuditPackageManifestId &&
                    item.ManifestSha256 == version.Manifest.ManifestSha256);
                if (request.DeliveredAt < approval.ApprovedAt)
                    return PackageChanges.Conflict("The external handoff cannot precede its exact disclosure approval.");
                NativeFactReference? buildProof = null;
                NativeFactReference? disclosureProof = null;
                var validationProofs = new List<NativeFactReference>();
                var validation = version.Validations[^1];
                foreach (var fact in snapshot.History.Facts)
                {
                    if (fact is not IAuditPackageManifestFact native || native.PackageVersion != request.PackageVersion)
                        continue;
                    if (fact is AuditPackageBuilt)
                    {
                        var built = await PackageContent.ReadAsync<AuditPackageBuiltChange>(content, request.TenantId,
                            native.Change, token).ConfigureAwait(false);
                        if (built.BuildId == build.BuildAttemptId)
                            buildProof = NativeFactReference.Of(snapshot.Owner.Stream, fact);
                    }
                    if (fact is AuditPackageDisclosureApproved)
                    {
                        var approved = await PackageContent.ReadAsync<AuditPackageDisclosureApprovalView>(content,
                            request.TenantId, native.Change, token).ConfigureAwait(false);
                        if (approved.DisclosureApprovalId == approval.DisclosureApprovalId)
                            disclosureProof = NativeFactReference.Of(snapshot.Owner.Stream, fact);
                    }
                    if (fact is AuditPackageValidated)
                    {
                        var checkedValidation = await PackageContent.ReadAsync<AuditPackageValidationView>(content,
                            request.TenantId, native.Change, token).ConfigureAwait(false);
                        if (checkedValidation.AuditPackageValidationId == validation.AuditPackageValidationId)
                        {
                            validationProofs.Clear();
                            validationProofs.Add(NativeFactReference.Of(snapshot.Owner.Stream, fact));
                        }
                    }
                    if (fact is AuditPackageValidationOverrideApproved)
                    {
                        var approved = await PackageContent.ReadAsync<AuditPackageValidationOverrideChange>(content,
                            request.TenantId, native.Change, token).ConfigureAwait(false);
                        if (approved.ValidationId == validation.AuditPackageValidationId &&
                            validation.Overrides.Any(item => item == approved.Override))
                            validationProofs.Add(NativeFactReference.Of(snapshot.Owner.Stream, fact));
                    }
                }
                if (buildProof is null || disclosureProof is null || validationProofs.Count == 0)
                    return PackageChanges.Conflict("The package's exact native approval proofs are incomplete.");
                var local = AuditPackagePreparedProof.ForDelivery(snapshot.History, request.PackageVersion,
                    buildProof, disclosureProof, validationProofs);
                if (!local.IsSuccess)
                    return Result<PreparedPackageDelta>.Failure(local.Error!);
                var proofs = await PackageContent.WriteAsync<IReadOnlyList<NativeFactReference>>(content,
                    request.TenantId, "audit-package-validation-proof.v1", validationProofs, token).ConfigureAwait(false);
                var intent = new PackageDeliveryRecordingIntent(PackageContent.Header(operation), request.DeliveryId,
                    request.PackageId, request.PackageVersion, version.Manifest.AuditPackageManifestId,
                    version.Manifest.ManifestSha256, build.ArchiveArtifactId!.Value, build.ArchiveSha256!,
                    request.Recipient.Trim(), request.DeliveryReference.Trim(), request.DeliveredAt, operation.AcceptedAt,
                    PackageChanges.Member(operation), disclosureProof, new ImmutableSelectionManifest(proofs,
                        "audit-package-validation-proof.v1", validationProofs.Count, SelectionOrder.CanonicalKey, proofs.Sha256));
                var frozen = await PackageContent.WriteAsync(content, request.TenantId,
                    PackageDeliveryRecordingIntent.Schema, intent, token).ConfigureAwait(false);
                return Result<PreparedPackageDelta>.Success(new PreparedPackageDelta(frozen, buildProof, disclosureProof,
                    validationProofs, local.Value));
            }, (owner, operation, delta, response, _) => owner.AdmitDelivery(request.ProgramId,
                request.ExpectedManifestRevision, request.PackageVersion, operation, delta.Content, delta.Build!,
                delta.Disclosure!, delta.Validations!, response, delta.Proof!), true, ct).ConfigureAwait(false);
        return await operations.AcknowledgmentAsync(request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
