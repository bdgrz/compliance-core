using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

/// <summary>Freezes exact selected bytes and an authored manifest revision before queueing one logical build.</summary>
public sealed class RequestAuditPackageBuildHandler(AuditPackageRootOperation operations, IImmutableContentStore content)
    : IRequestHandler<RequestAuditPackageBuild, OperationAcceptedResponse>
{
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RequestAuditPackageBuild> context, CancellationToken ct)
    {
        var request = context.Request;
        if (string.IsNullOrWhiteSpace(request.BuildKey) || request.PackageVersion is < 1 or > int.MaxValue)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A build requires a stable key and exact manifest version."));
        var buildId = AuditPackageCommandIdentity.Build(request.PackageId, request.PackageVersion, request.BuildKey);
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            buildId, nameof(RequestAuditPackageBuild), request.ExpectedManifestRevision, _ => request.PackageVersion,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedManifestRevision, request.PackageVersion),
            async (snapshot, operation, token) =>
            {
                var view = snapshot.View!;
                if (view.ExaminationId != request.ExaminationId || view.PackageKind != request.PackageKind)
                    return PackageChanges.Conflict("The build must identify the package's exact examination and kind.");
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId, view);
                if (rules.RequestBuild(request.ExpectedManifestRevision, request.BuildKey, PackageChanges.Member(operation),
                        operation.Actor.Display, operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                var version = view.Versions[^1];
                var manifest = await PackageContent.WriteAsync(content, request.TenantId, "audit-package-manifest.v1",
                    version.Manifest, token).ConfigureAwait(false);
                var selected = version.Manifest.Entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
                    .Select(entry => entry.Source).ToArray();
                var selection = await PackageContent.WriteAsync<IReadOnlyList<AuditPackageSelectionEntry>>(content,
                    request.TenantId, "audit-package-selection-entry.v1", selected, token).ConfigureAwait(false);
                var intent = new PackageBuildIntent(PackageContent.Header(operation), request.PackageId,
                    view.ExaminationId, view.PackageKind, request.PackageVersion, buildId, request.BuildKey.Trim(),
                    version.Manifest.AuditPackageManifestId, version.Manifest.ManifestSha256, manifest,
                    new ImmutableSelectionManifest(selection, "audit-package-selection-entry.v1", selected.Length,
                        SelectionOrder.CanonicalKey, selection.Sha256));
                return Result<PreparedPackageDelta>.Success(await PackageContent.DeltaAsync(content, request.TenantId,
                    PackageBuildIntent.Schema, intent, token).ConfigureAwait(false));
            }, (owner, operation, delta, response, _) => owner.AdmitBuild(request.ProgramId,
                request.ExpectedManifestRevision, request.PackageVersion, operation, delta.Content, response), true, ct)
            .ConfigureAwait(false);
        return await operations.AcknowledgmentAsync(request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
