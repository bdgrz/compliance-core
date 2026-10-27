using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.History;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Rules;

/// <summary>Verifies source facts on their authoritative owner, then binds every frozen build/delivery field.</summary>
public sealed class AuditPackageSourceVerifier(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
{
    public async ValueTask<Result<VerifiedPackageBuild>> BuildAsync(Uuid tenant, Uuid packageId, Uuid buildId,
        long version, Uuid operationId, NativeFactReference source, ImmutableContentRef reference, CancellationToken ct)
    {
        if (reference.Schema != PackageBuildIntent.Schema)
            return BuildFailure("The build intent has another schema.");
        var owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, packageId, selectedVersion: version), ct).ConfigureAwait(false);
        var history = await AuditPackageManifestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (source.Stream != owner.Stream)
            return BuildFailure("The build proof belongs to another manifest owner.");
        var sourceFact = history.FactById(source.EventId);
        if (sourceFact is null)
            return BuildFailure("The exact admitted build is not visible yet.", true);
        if (sourceFact is not AuditPackageBuildRequested admitted)
            return BuildFailure("The source fact is not a package build admission.");
        if (admitted.Change != reference)
            return BuildFailure("The build intent differs from its exact source admission.");
        var intent = await PackageContent.ReadAsync<PackageBuildIntent>(content, tenant, reference, ct)
            .ConfigureAwait(false);
        if (admitted.Metadata.AggregateVersion != source.AggregateVersion || admitted.Change != reference ||
            admitted.PackageVersion != version || admitted.Operation.LogicalId != buildId ||
            intent.PackageId != packageId || intent.PackageVersion != version || intent.BuildId != buildId ||
            operationId != buildId || !HeaderMatches(intent.Header, admitted.Operation) ||
            intent.BuildId != AuditPackageCommandIdentity.Build(packageId, version, intent.BuildKey))
            return BuildFailure("The build differs from its exact immutable admission.");
        var manifest = await PackageContent.ReadAsync<AuditPackageManifestView>(content, tenant, intent.Manifest, ct)
            .ConfigureAwait(false);
        var selection = await PackageContent.ReadAsync<IReadOnlyList<AuditPackageSelectionEntry>>(content, tenant,
            intent.Selection.Content, ct).ConfigureAwait(false);
        var expected = manifest.Entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .Select(entry => entry.Source).ToArray();
        if (manifest.TenantId != tenant || manifest.ProgramId != admitted.ProgramId || manifest.AuditPackageId != packageId ||
            manifest.PackageVersion != version || manifest.AuditPackageManifestId != intent.ManifestId ||
            manifest.ManifestSha256 != intent.ManifestSha256 || intent.Selection.Count != selection.Count ||
            intent.Selection.ItemsSha256 != intent.Selection.Content.Sha256 ||
            intent.Selection.ItemSchema != "audit-package-selection-entry.v1" ||
            intent.Selection.Content.Schema != intent.Selection.ItemSchema || !selection.SequenceEqual(expected))
            return BuildFailure("The complete build manifest and selection do not match their admitted identity.");
        return Result<VerifiedPackageBuild>.Success(new VerifiedPackageBuild(owner, admitted, intent, manifest, history));
    }

    public async ValueTask<Result<PackageDeliveryRecordingIntent>> DeliveryAsync(Uuid tenant, Uuid packageId,
        Uuid deliveryId, long version, Uuid operationId, NativeFactReference source, ImmutableContentRef reference,
        CancellationToken ct)
    {
        if (reference.Schema != PackageDeliveryRecordingIntent.Schema)
            return DeliveryFailure("The delivery intent has another schema.");
        var owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, packageId, selectedVersion: version), ct).ConfigureAwait(false);
        var history = await AuditPackageManifestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (source.Stream != owner.Stream)
            return DeliveryFailure("The handoff proof belongs to another manifest owner.");
        var sourceFact = history.FactById(source.EventId);
        if (sourceFact is null)
            return DeliveryFailure("The exact delivery admission is not visible yet.", true);
        if (sourceFact is not AuditPackageDeliveryAdmitted admitted)
            return DeliveryFailure("The source fact is not a personal delivery admission.");
        if (admitted.Intent != reference)
            return DeliveryFailure("The handoff intent differs from its exact source admission.");
        var intent = await PackageContent.ReadAsync<PackageDeliveryRecordingIntent>(content, tenant, reference, ct)
            .ConfigureAwait(false);
        if (admitted.Metadata.AggregateVersion != source.AggregateVersion || admitted.Intent != reference ||
            admitted.PackageVersion != version || admitted.Operation.LogicalId != deliveryId || operationId != deliveryId ||
            intent.PackageId != packageId || intent.PackageVersion != version || intent.DeliveryId != deliveryId ||
            intent.RecordedAt != admitted.OccurredAt || intent.RecordedAt != intent.Header.AcceptedAt ||
            !HeaderMatches(intent.Header, admitted.Operation) ||
            intent.Header.OriginalActor.Kind != "member" || intent.Header.OriginalActor.Id != intent.RecorderMemberId.ToString() ||
            intent.DeliveredAt == default || intent.DeliveredAt > intent.RecordedAt ||
            deliveryId != AuditPackageCommandIdentity.Delivery(packageId, version, intent.Recipient, intent.DeliveryReference))
            return DeliveryFailure("The delivery differs from its exact admitted personal handoff.");
        if (intent.DisclosureApproval.Stream != owner.Stream ||
            history.FactById(intent.DisclosureApproval.EventId) is not AuditPackageDisclosureApproved approvalFact ||
            approvalFact.Metadata.AggregateVersion != intent.DisclosureApproval.AggregateVersion ||
            approvalFact.PackageVersion != version || approvalFact.Revision >= admitted.Revision)
            return DeliveryFailure("The handoff does not pin an earlier disclosure on this exact manifest.");
        var approval = await PackageContent.ReadAsync<AuditPackageDisclosureApprovalView>(content, tenant,
            approvalFact.Change, ct).ConfigureAwait(false);
        if (approval.ManifestId != intent.ManifestId || approval.ManifestSha256 != intent.ManifestSha256 ||
            approval.Recipient != intent.Recipient || intent.DeliveredAt < approval.ApprovedAt)
            return DeliveryFailure("The handoff differs from its exact approved recipient, manifest or time.");
        var proofs = await PackageContent.ReadAsync<IReadOnlyList<NativeFactReference>>(content, tenant,
            intent.ValidationProofs.Content, ct).ConfigureAwait(false);
        if (proofs.Count == 0 || intent.ValidationProofs.Count != proofs.Count ||
            intent.ValidationProofs.ItemsSha256 != intent.ValidationProofs.Content.Sha256 ||
            proofs.Any(proof => proof.Stream != owner.Stream || history.FactById(proof.EventId) is not IAuditPackageManifestFact fact ||
                fact.PackageVersion != version || fact.Revision >= admitted.Revision ||
                ((DomainEvent)fact).Metadata.AggregateVersion != proof.AggregateVersion))
            return DeliveryFailure("The complete validation proof manifest does not match this handoff admission.");
        var latest = history.Facts.OfType<AuditPackageValidated>().LastOrDefault(fact =>
            fact.PackageVersion == version && fact.Revision < admitted.Revision);
        if (latest is null || proofs.All(proof => proof.EventId != latest.Metadata.EventId))
            return DeliveryFailure("The handoff omits the latest applicable validation.");
        var validation = await PackageContent.ReadAsync<AuditPackageValidationView>(content, tenant, latest.Change, ct)
            .ConfigureAwait(false);
        if (validation.ManifestId != intent.ManifestId || validation.ManifestSha256 != intent.ManifestSha256)
            return DeliveryFailure("The validation concerns another exact manifest.");
        var overrides = new List<AuditPackageValidationOverrideView>();
        foreach (var proof in proofs.Where(proof => proof.EventId != latest.Metadata.EventId))
        {
            if (history.FactById(proof.EventId) is not AuditPackageValidationOverrideApproved fact)
                return DeliveryFailure("The validation manifest contains an unrelated native fact.");
            var change = await PackageContent.ReadAsync<AuditPackageValidationOverrideChange>(content, tenant,
                fact.Change, ct).ConfigureAwait(false);
            if (change.ValidationId != validation.AuditPackageValidationId ||
                change.Override.ApprovedByMemberId == validation.ValidatedByMemberId ||
                !validation.Findings.Any(finding => finding.FindingId == change.Override.FindingId &&
                    finding.BlocksDelivery && finding.OverrideAllowed))
                return DeliveryFailure("A handoff override is unrelated, ineligible or not independently approved.");
            overrides.Add(change.Override);
        }
        if (validation.Findings.Any(finding => finding.BlocksDelivery &&
                !overrides.Any(approved => approved.FindingId == finding.FindingId)))
            return DeliveryFailure("The handoff has an unresolved blocking validation finding.");
        var exactBuild = false;
        foreach (var builtFact in history.Facts.OfType<AuditPackageBuilt>().Where(fact =>
                     fact.PackageVersion == version && fact.Revision < admitted.Revision))
        {
            var built = await PackageContent.ReadAsync<AuditPackageBuiltChange>(content, tenant, builtFact.Change, ct)
                .ConfigureAwait(false);
            exactBuild |= built.Output.ManifestId == intent.ManifestId && built.Output.ManifestSha256 == intent.ManifestSha256 &&
                built.Output.ArchiveArtifactId == intent.ArchiveArtifactId && built.Output.ArchiveSha256 == intent.ArchiveSha256 &&
                built.Output.ArchiveBytes is > 0 && built.Output.FailureCode is null;
        }
        if (!exactBuild)
            return DeliveryFailure("The historical handoff does not match an earlier exact successful archive proof.");
        return Result<PackageDeliveryRecordingIntent>.Success(intent);
    }

    static bool HeaderMatches(DurableIntentHeader header, OperationProvenance operation) =>
        header.RootOperationId == operation.RootOperationId && header.FirstOwner == operation.Owner &&
        header.AffectedOwner == operation.AffectedOwner && header.ExpectedRevisionOwner == operation.ExpectedRevisionOwner &&
        header.OriginalActor == operation.Actor && header.Episode == operation.Episode &&
        header.OriginalExpectedRevision == operation.OriginalExpectedRevision &&
        header.AuthoredIntentSha256 == operation.AuthoredIntentSha256 && header.AcceptedAt == operation.AcceptedAt;
    static Result<VerifiedPackageBuild> BuildFailure(string message, bool transient = false) =>
        Result<VerifiedPackageBuild>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
    static Result<PackageDeliveryRecordingIntent> DeliveryFailure(string message, bool transient = false) =>
        Result<PackageDeliveryRecordingIntent>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
