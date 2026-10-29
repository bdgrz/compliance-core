using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

/// <summary>Admits one complete next-version manifest; exact retries bypass later source changes.</summary>
public sealed class AmendAuditPackageHandler(AuditPackageRootOperation operations, AuditPackageSourceBuilder sources,
    IImmutableContentStore content) : IRequestHandler<AmendAuditPackage>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AmendAuditPackage> context, CancellationToken ct)
    {
        var request = context.Request;
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            request.CommandId, nameof(AmendAuditPackage), request.ExpectedRevision, owner => owner.CurrentVersion + 1,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedRevision, owner.CurrentVersion,
                allowFrozen: true), async (snapshot, operation, token) =>
            {
                var view = snapshot.View!;
                var prior = view.Versions[^1];
                if ((request.Content is null) == (request.IncludedRecordIds is null))
                    return PackageChanges.Invalid("An amendment requires exactly one complete or retained selection.");
                AuditPackageContent selected;
                if (request.Content is { } complete)
                {
                    if (complete.ExaminationId != view.ExaminationId || complete.PackageKind != view.PackageKind ||
                        complete.Name != view.Name || complete.SourcePeriodId != prior.SourcePeriodId ||
                        complete.SourceSnapshotKind != prior.SourceSnapshotKind)
                        return PackageChanges.Conflict("An amendment must preserve package identity and source kind/period.");
                    selected = complete;
                }
                else
                {
                    var ids = request.IncludedRecordIds!;
                    if (ids.Count == 0 || ids.Any(id => id == Uuid.Empty) || ids.Distinct().Count() != ids.Count)
                        return PackageChanges.Invalid("An amendment requires unique nonempty selected identities.");
                    var retained = prior.SelectedContent.Where(item => ids.Contains(item.SourceRecordId)).ToArray();
                    if (retained.Select(item => item.SourceRecordId).Distinct().Count() != ids.Count)
                        return PackageChanges.Conflict("An amendment may retain only records in its exact prior selection.");
                    selected = PackageChanges.Content(view, prior) with { SelectedContent = retained };
                }
                var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId, selected, token)
                    .ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result<PreparedPackageDelta>.Failure(resolved.Error!);
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId, view);
                if (rules.Amend(request.ExpectedRevision, request.AmendmentReason, resolved.Value!.SourceSnapshotId,
                        resolved.Value.SourceSnapshotSha256, resolved.Value.SelectedContent, PackageChanges.Member(operation),
                        operation.Actor.Display, operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                return Result<PreparedPackageDelta>.Success(await PackageContent.DeltaAsync(content, request.TenantId,
                    "AuditPackageAmended.change.v1", PackageChanges.Version(rules.Current!, operation), token)
                    .ConfigureAwait(false));
            }, (owner, operation, delta, response, result) => owner.Amend(request.ProgramId,
                request.ExpectedRevision, operation, delta.Content, response, result), false, ct).ConfigureAwait(false);
        return AuditPackageRootOperation.Unit(outcome);
    }
}
