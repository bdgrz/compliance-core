using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

/// <summary>Records an attributable exact-recipient approval against the manifest's authored revision.</summary>
public sealed class ApproveAuditPackageDisclosureHandler(AuditPackageRootOperation operations,
    IImmutableContentStore content) : IRequestHandler<ApproveAuditPackageDisclosure>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveAuditPackageDisclosure> context, CancellationToken ct)
    {
        var request = context.Request;
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            request.CommandId, nameof(ApproveAuditPackageDisclosure), request.ExpectedRevision, _ => request.PackageVersion,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedRevision, request.PackageVersion),
            async (snapshot, operation, token) =>
            {
                if (string.IsNullOrWhiteSpace(request.Recipient) || string.IsNullOrWhiteSpace(request.Purpose))
                    return PackageChanges.Invalid("Disclosure approval requires an exact recipient and purpose.");
                if (snapshot.View!.Versions[^1].DisclosureApprovals.Any(item =>
                        item.Recipient == request.Recipient.Trim() && item.Purpose == request.Purpose.Trim()))
                    return PackageChanges.Conflict("This exact recipient approval exists; replay its original command id.");
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId, snapshot.View);
                if (rules.ApproveDisclosure(request.ExpectedRevision, checked((int)request.PackageVersion),
                        request.Recipient, request.Purpose, request.Rationale, PackageChanges.Member(operation),
                        operation.Actor.Display, operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                var approval = rules.Current!.Versions[^1].DisclosureApprovals.Last(item =>
                    item.Recipient == request.Recipient.Trim() && item.Purpose == request.Purpose.Trim());
                return Result<PreparedPackageDelta>.Success(await PackageContent.DeltaAsync(content, request.TenantId,
                    "AuditPackageDisclosureApproved.change.v1", approval, token).ConfigureAwait(false));
            }, (owner, operation, delta, response, result) => owner.ApproveDisclosure(request.ProgramId,
                request.ExpectedRevision, request.PackageVersion, operation, delta.Content, response, result), false, ct)
            .ConfigureAwait(false);
        return AuditPackageRootOperation.Unit(outcome);
    }
}
