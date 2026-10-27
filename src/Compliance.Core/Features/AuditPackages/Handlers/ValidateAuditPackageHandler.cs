using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

/// <summary>Validates the exact source selection and actual stored archive; proof absence never passes validation.</summary>
public sealed class ValidateAuditPackageHandler(AuditPackageRootOperation operations, AuditPackageSourceBuilder sources,
    IArtifactContentStore artifacts, IImmutableContentStore content) : IRequestHandler<ValidateAuditPackage>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ValidateAuditPackage> context, CancellationToken ct)
    {
        var request = context.Request;
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            request.CommandId, nameof(ValidateAuditPackage), request.ExpectedRevision, _ => request.PackageVersion,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedRevision, request.PackageVersion),
            async (snapshot, operation, token) =>
            {
                if (string.IsNullOrWhiteSpace(request.ValidationKey))
                    return PackageChanges.Invalid("Validation requires a nonempty stable key.");
                var view = snapshot.View!;
                var version = view.Versions[^1];
                if (version.Validations.Any(item => item.ValidationKey == request.ValidationKey.Trim()))
                    return PackageChanges.Conflict("The validation key already exists; replay its original command id.");
                var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId,
                    PackageChanges.Content(view, version), token).ConfigureAwait(false);
                var failureCode = resolved.IsSuccess ? null : "frozen_source_mismatch";
                if (failureCode is null)
                    failureCode = version.BuildAttempts.LastOrDefault(attempt => attempt.Status == "built") is { } build
                        ? await AuditPackageArchiveVerifier.FailureCodeAsync(artifacts, request.TenantId, build,
                            version.Manifest, token).ConfigureAwait(false) : "archive_content_unavailable";
                AuditPackageValidationFinding[] findings = failureCode is null ? [] :
                    [new AuditPackageValidationFinding(Uuid.CreateVersion5(request.PackageId,
                        $"{request.PackageVersion}\n{request.ValidationKey}\n{failureCode}"), failureCode, "error", true,
                        false, "The exact frozen source or actual archive did not verify: " + failureCode)];
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId, view);
                if (rules.RecordValidation(request.ExpectedRevision, checked((int)request.PackageVersion),
                        request.ValidationKey, findings, PackageChanges.Member(operation), operation.Actor.Display,
                        operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                var validation = rules.Current!.Versions[^1].Validations.Last(item =>
                    item.ValidationKey == request.ValidationKey.Trim());
                return Result<PreparedPackageDelta>.Success(await PackageContent.DeltaAsync(content, request.TenantId,
                    "AuditPackageValidated.change.v1", validation, token).ConfigureAwait(false));
            }, (owner, operation, delta, response, result) => owner.RecordValidation(request.ProgramId,
                request.ExpectedRevision, request.PackageVersion, operation, delta.Content, response, result), false, ct)
            .ConfigureAwait(false);
        return AuditPackageRootOperation.Unit(outcome);
    }
}
