using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit native fixture construction from a rule-composed view; no runtime legacy read/write adapter exists.</summary>
static class PackageSeeds
{
    public static Task WriteAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, AuditPackageView view, CancellationToken ct = default) =>
        SaveAsync(reader, writer, events, content, view, ct);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, AuditPackageView view, CancellationToken ct = default) =>
        SaveAsync(reader, writer, events, content, view, ct);

    public static void Configure(IServiceCollection services, IDomainEventReader events)
    {
        var original = services.Last(item => !item.IsKeyedService && item.ServiceType == typeof(IAggregateReader));
        var key = new object();
        services.Remove(original);
        if (original.ImplementationInstance is { } instance)
            services.Add(ServiceDescriptor.KeyedSingleton(typeof(IAggregateReader), key, instance));
        else if (original.ImplementationFactory is { } factory)
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key,
                (provider, _) => factory(provider), original.Lifetime));
        else
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key,
                original.ImplementationType!, original.Lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(IAggregateReader), provider =>
            new AuditPackageManifestAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(key), events),
            original.Lifetime));
    }

    static async Task SaveAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, AuditPackageView view, CancellationToken ct)
    {
        var tenant = view.TenantId;
        var owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, view.AuditPackageId), ct);
        var history = await AuditPackageManifestHistory.ReadAsync(events, owner, ct);
        async Task SelectAsync(OperationProvenance operation, long version)
        {
            owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, view.AuditPackageId,
                operation.LogicalId, operation.CommandKind, version), ct);
            history = await AuditPackageManifestHistory.ReadAsync(events, owner, ct);
        }
        async Task SaveOwnerAsync()
        {
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
            owner = await reader.HydrateAsync(new AuditPackageManifest(tenant, view.AuditPackageId), ct);
            history = await AuditPackageManifestHistory.ReadAsync(events, owner, ct);
        }
        var unit = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), UnitResult.Schema,
            new UnitResult(), ct);
        var unitResult = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
            "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct);
        var response = await FirstResponses.FreezeAsync(content, tenant.ToString(), new FirstResponseDescriptor(200,
            ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(unit)), ct);
        foreach (var version in view.Versions)
        {
            var actor = ActorReference.ForMember(version.CreatedByMemberId, version.CreatedByDisplay);
            if (!owner.HasVersion(version.PackageVersion))
            {
                var operation = Root(version.PackageVersion == 1 ? view.AuditPackageId : Uuid.CreateVersion4(),
                    version.PackageVersion == 1 ? nameof(CreateAuditPackage) : nameof(AmendAuditPackage), actor,
                    owner, version.PackageVersion, version.CreatedAt);
                var data = new AuditPackageContent(view.ExaminationId, view.PackageKind, version.SourceSnapshotKind,
                    version.SourceSnapshotId, version.SourceSnapshotSha256, view.Name, version.SelectedContent,
                    version.SourcePeriodId);
                var change = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                    version.PackageVersion == 1 ? "AuditPackageCreated.change.v1" : "AuditPackageAmended.change.v1",
                    new AuditPackageManifestVersionContent(view.ProgramId, data, version.Manifest,
                        version.AmendsVersion, version.AmendmentReason, actor, version.CreatedAt), ct);
                await SelectAsync(operation, version.PackageVersion);
                Assert.True((version.PackageVersion == 1
                    ? owner.Create(view.ProgramId, operation, change, response, unitResult)
                    : owner.Amend(view.ProgramId, owner.Revision, operation, change, response, unitResult)).IsSuccess);
                await SaveOwnerAsync();
            }
            foreach (var build in version.BuildAttempts)
            {
                var admitted = history.Facts.OfType<AuditPackageBuildRequested>().FirstOrDefault(fact =>
                    fact.Operation.LogicalId == build.BuildAttemptId);
                if (admitted is null)
                {
                    var operation = Root(build.BuildAttemptId, nameof(RequestAuditPackageBuild),
                        ActorReference.ForMember(build.RequestedByMemberId ?? version.CreatedByMemberId,
                            build.RequestedByDisplay), owner, version.PackageVersion, build.RequestedAt);
                    var manifest = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        "audit-package-manifest.v1", version.Manifest, ct);
                    var rows = version.Manifest.Entries
                        .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).Select(entry => entry.Source).ToArray();
                    var selection = await content.WriteJsonAsync<IReadOnlyList<AuditPackageSelectionEntry>>(tenant.ToString(), Uuid.CreateVersion4(),
                        "audit-package-selection-entry.v1", rows, ct);
                    var intent = new PackageBuildIntent(Header(operation), view.AuditPackageId, view.ExaminationId,
                        view.PackageKind, version.PackageVersion, build.BuildAttemptId, build.BuildKey,
                        version.Manifest.AuditPackageManifestId, version.Manifest.ManifestSha256, manifest,
                        new ImmutableSelectionManifest(selection, "audit-package-selection-entry.v1", rows.Length,
                            SelectionOrder.CanonicalKey, selection.Sha256));
                    var frozen = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        PackageBuildIntent.Schema, intent, ct);
                    await SelectAsync(operation, version.PackageVersion);
                    Assert.True(owner.AdmitBuild(view.ProgramId, owner.Revision, version.PackageVersion, operation,
                        frozen, response).IsSuccess);
                    await SaveOwnerAsync();
                    admitted = history.Facts.OfType<AuditPackageBuildRequested>().Last();
                }
                if (build.Status != "built" || history.Facts.OfType<AuditPackageBuilt>().Any(fact =>
                        fact.Operation.RootOperationId == build.BuildAttemptId))
                    continue;
                var source = NativeFactReference.Of(owner.Stream, admitted);
                var operationBuilt = SourceUseReceiverSupport.Child(Uuid.CreateVersion5(source.EventId, "seed-result"),
                    nameof(RecordAuditPackageBuildResult), source.EventId, Header(admitted.Operation), owner.Stream,
                    build.CompletedAt ?? build.UpdatedAt);
                var output = new AuditPackageBuildOutput(version.Manifest.AuditPackageManifestId,
                    version.Manifest.ManifestSha256, version.Manifest.Entries, build.ArchiveArtifactId,
                    build.ArchiveSha256, build.ArchiveBytes, null);
                var successful = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                    "AuditPackageBuilt.change.v1", new AuditPackageBuiltChange(build.BuildAttemptId, output,
                        build.CompletedAt ?? build.UpdatedAt), ct);
                await SelectAsync(operationBuilt, version.PackageVersion);
                var buildProof = AuditPackagePreparedProof.ForBuild(history, source, version.PackageVersion, build.BuildAttemptId);
                Assert.True(buildProof.IsSuccess);
                Assert.True(owner.RecordBuilt(view.ProgramId, owner.Revision, version.PackageVersion, operationBuilt,
                    source, successful, unitResult, buildProof.Value!).IsSuccess);
                await SaveOwnerAsync();
            }
            foreach (var validation in version.Validations)
            {
                var existing = new List<AuditPackageValidated>();
                foreach (var fact in history.Facts.OfType<AuditPackageValidated>())
                {
                    var data = await content.ReadJsonAsync<AuditPackageValidationView>(tenant.ToString(), fact.Change, ct);
                    if (data.AuditPackageValidationId == validation.AuditPackageValidationId)
                        existing.Add(fact);
                }
                if (existing.Count == 0)
                {
                    var operation = Root(Uuid.CreateVersion4(), nameof(ValidateAuditPackage),
                        ActorReference.ForMember(validation.ValidatedByMemberId, validation.ValidatedByDisplay), owner,
                        version.PackageVersion, validation.UpdatedAt);
                    var initial = validation with
                    {
                        Overrides = [],
                        Status = validation.Findings.Any(finding => finding.BlocksDelivery) ? "blocked" : "passed"
                    };
                    var change = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        "AuditPackageValidated.change.v1", initial, ct);
                    await SelectAsync(operation, version.PackageVersion);
                    Assert.True(owner.RecordValidation(view.ProgramId, owner.Revision, version.PackageVersion,
                        operation, change, response, unitResult).IsSuccess);
                    await SaveOwnerAsync();
                }
                foreach (var approved in validation.Overrides)
                {
                    var duplicate = false;
                    foreach (var fact in history.Facts.OfType<AuditPackageValidationOverrideApproved>())
                    {
                        var data = await content.ReadJsonAsync<AuditPackageValidationOverrideChange>(tenant.ToString(),
                            fact.Change, ct);
                        duplicate |= data.ValidationId == validation.AuditPackageValidationId && data.Override == approved;
                    }
                    if (duplicate)
                        continue;
                    var operation = Root(Uuid.CreateVersion4(), nameof(ApproveAuditPackageValidationOverride),
                        ActorReference.ForMember(approved.ApprovedByMemberId, approved.ApprovedByDisplay), owner,
                        version.PackageVersion, approved.ApprovedAt);
                    var change = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        "AuditPackageValidationOverrideApproved.change.v1",
                        new AuditPackageValidationOverrideChange(validation.AuditPackageValidationId, approved), ct);
                    await SelectAsync(operation, version.PackageVersion);
                    Assert.True(owner.ApproveOverride(view.ProgramId, owner.Revision, version.PackageVersion,
                        operation, change, response, unitResult).IsSuccess);
                    await SaveOwnerAsync();
                }
            }
            foreach (var approval in version.DisclosureApprovals)
            {
                var duplicate = false;
                foreach (var fact in history.Facts.OfType<AuditPackageDisclosureApproved>())
                {
                    var data = await content.ReadJsonAsync<AuditPackageDisclosureApprovalView>(tenant.ToString(),
                        fact.Change, ct);
                    duplicate |= data.DisclosureApprovalId == approval.DisclosureApprovalId;
                }
                if (duplicate)
                    continue;
                var operation = Root(Uuid.CreateVersion4(), nameof(ApproveAuditPackageDisclosure),
                    ActorReference.ForMember(approval.ApprovedByMemberId, approval.ApprovedByDisplay), owner,
                    version.PackageVersion, approval.ApprovedAt);
                var change = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                    "AuditPackageDisclosureApproved.change.v1", approval, ct);
                await SelectAsync(operation, version.PackageVersion);
                Assert.True(owner.ApproveDisclosure(view.ProgramId, owner.Revision, version.PackageVersion,
                    operation, change, response, unitResult).IsSuccess);
                await SaveOwnerAsync();
            }
            foreach (var delivery in version.Deliveries)
            {
                var admission = history.Facts.OfType<AuditPackageDeliveryAdmitted>().FirstOrDefault(fact =>
                    fact.Operation.LogicalId == delivery.AuditPackageDeliveryId);
                PackageDeliveryRecordingIntent intent;
                if (admission is null)
                {
                    var disclosure = history.Facts.OfType<AuditPackageDisclosureApproved>().Last(fact =>
                        fact.PackageVersion == version.PackageVersion);
                    var build = history.Facts.OfType<AuditPackageBuilt>().Last(fact =>
                        fact.PackageVersion == version.PackageVersion);
                    var latest = history.Facts.OfType<AuditPackageValidated>().Last(fact =>
                        fact.PackageVersion == version.PackageVersion);
                    IReadOnlyList<NativeFactReference> proofs = [NativeFactReference.Of(owner.Stream, latest),
                        .. history.Facts.OfType<AuditPackageValidationOverrideApproved>().Where(fact =>
                            fact.PackageVersion == version.PackageVersion && fact.Revision > latest.Revision)
                            .Select(fact => NativeFactReference.Of(owner.Stream, fact))];
                    var proofManifest = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        "audit-package-validation-proof.v1", proofs, ct);
                    var operation = Root(delivery.AuditPackageDeliveryId, nameof(RecordAuditPackageDelivery),
                        ActorReference.ForMember(delivery.ActorMemberId ?? version.CreatedByMemberId, delivery.ActorDisplay),
                        owner, version.PackageVersion, delivery.UpdatedAt);
                    intent = new PackageDeliveryRecordingIntent(Header(operation), delivery.AuditPackageDeliveryId,
                        view.AuditPackageId, version.PackageVersion, delivery.ManifestId, delivery.ManifestSha256,
                        delivery.ArchiveArtifactId, delivery.ArchiveSha256, delivery.Recipient, delivery.DeliveryReference,
                        delivery.UpdatedAt, delivery.UpdatedAt, delivery.ActorMemberId ?? version.CreatedByMemberId,
                        NativeFactReference.Of(owner.Stream, disclosure), new ImmutableSelectionManifest(proofManifest,
                            "audit-package-validation-proof.v1", proofs.Count, SelectionOrder.CanonicalKey, proofManifest.Sha256));
                    var frozen = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        PackageDeliveryRecordingIntent.Schema, intent, ct);
                    await SelectAsync(operation, version.PackageVersion);
                    var deliveryProof = AuditPackagePreparedProof.ForDelivery(history, version.PackageVersion,
                        NativeFactReference.Of(owner.Stream, build), NativeFactReference.Of(owner.Stream, disclosure), proofs);
                    Assert.True(deliveryProof.IsSuccess);
                    Assert.True(owner.AdmitDelivery(view.ProgramId, owner.Revision, version.PackageVersion, operation,
                        frozen, NativeFactReference.Of(owner.Stream, build), NativeFactReference.Of(owner.Stream, disclosure),
                        proofs, response, deliveryProof.Value!).IsSuccess);
                    await SaveOwnerAsync();
                    admission = history.Facts.OfType<AuditPackageDeliveryAdmitted>().Last();
                }
                else
                    intent = await content.ReadJsonAsync<PackageDeliveryRecordingIntent>(tenant.ToString(), admission.Intent, ct);
                var commandId = Uuid.CreateVersion5(admission.Metadata.EventId, "seed-materialization");
                var child = new AuditPackageDelivery(tenant, view.AuditPackageId, version.PackageVersion,
                    delivery.AuditPackageDeliveryId, commandId);
                child = await reader.HydrateAsync(child, ct);
                if (child.IsMaterialized)
                    continue;
                var source = NativeFactReference.Of(owner.Stream, admission);
                var operationChild = SourceUseReceiverSupport.Child(commandId, nameof(MaterializeRecordedAuditPackageDelivery),
                    source.EventId, intent.Header, child.Stream, intent.RecordedAt);
                Assert.True(child.Materialize(view.ProgramId, operationChild, intent, admission.Intent, source, unitResult).IsSuccess);
                await writer.SaveAsync(child, new RequestDispatchContext(RequestActor.System), ct);
            }
        }
    }

    static OperationProvenance Root(Uuid logicalId, string kind, ActorReference actor, AuditPackageManifest owner,
        long version, DateTimeOffset at) => new(logicalId, logicalId, null, kind, actor, owner.Stream,
        version.ToString(CultureInfo.InvariantCulture), owner.Revision == 0 ? null : (ulong)owner.Revision,
        new string('a', 64), at, owner.Stream, owner.Stream, owner.Stream);
    static DurableIntentHeader Header(OperationProvenance operation) => new(operation.RootOperationId, operation.Owner,
        operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
        operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt);
}
