using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records an independent rationale-backed override of one exact overridable blocking finding.</summary>
public sealed class ApproveAuditPackageValidationOverrideHandler(AuditPackageRootOperation operations,
    IImmutableContentStore content) : IRequestHandler<ApproveAuditPackageValidationOverride>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveAuditPackageValidationOverride> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            request.CommandId, nameof(ApproveAuditPackageValidationOverride), request.ExpectedRevision,
            _ => request.PackageVersion,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedRevision, request.PackageVersion),
            async (snapshot, operation, token) =>
            {
                if (!Uuid.TryParse(request.ValidationIssue, CultureInfo.InvariantCulture, out var finding) ||
                    finding == Uuid.Empty)
                    return PackageChanges.Invalid("An override must identify one exact validation finding.");
                var validations = snapshot.View!.Versions[^1].Validations;
                if (validations.Count > 0 && validations[^1].Overrides.Any(item => item.FindingId == finding))
                    return PackageChanges.Conflict("This immutable finding override exists; replay its original command id.");
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId, snapshot.View);
                if (rules.ApproveValidationOverride(request.ExpectedRevision, checked((int)request.PackageVersion),
                        finding, request.Rationale, PackageChanges.Member(operation), operation.Actor.Display,
                        operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                var validation = rules.Current!.Versions[^1].Validations[^1];
                var change = new AuditPackageValidationOverrideChange(validation.AuditPackageValidationId,
                    validation.Overrides.Single(item => item.FindingId == finding));
                return Result<PreparedPackageDelta>.Success(await PackageContent.DeltaAsync(content, request.TenantId,
                    "AuditPackageValidationOverrideApproved.change.v1", change, token).ConfigureAwait(false));
            }, (owner, operation, delta, response, result) => owner.ApproveOverride(request.ProgramId,
                request.ExpectedRevision, request.PackageVersion, operation, delta.Content, response, result), false, ct)
            .ConfigureAwait(false);
        return AuditPackageRootOperation.Unit(outcome);
    }
}
