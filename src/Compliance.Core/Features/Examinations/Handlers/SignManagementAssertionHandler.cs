using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

/// <summary>Signs only as a current examination signer, binding the actual validated package and frozen source.</summary>
public sealed class SignManagementAssertionHandler(IAggregateReader reader, IAggregateExecutor executor,
    ExaminationSetupAccess access, AuditPackageReader packages, ExaminationEngagementReader baselines,
    TypeIIPeriodReader periods, TimeProvider clock) : IRequestHandler<SignManagementAssertion>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<SignManagementAssertion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.ExaminationId == Uuid.Empty ||
            request.AssertionId == Uuid.Empty || request.PackageId == Uuid.Empty || request.PackageVersion <= 0 ||
            request.AssertionVersion <= 0 || request.ExpectedRevision <= 0)
            return Conflict("Signing requires exact assertion, examination and package versions.");
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            RbacPermissions.TenantAccess, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var memberId = Uuid.Parse(actor.Value.Id);
        var examination = await reader.HydrateAsync(new Examination(request.TenantId, request.ExaminationId), ct).ConfigureAwait(false);
        if (examination.Current is not { } setup || setup.ProgramId != request.ProgramId || !examination.IsNamedSigner(memberId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a signer currently named on this examination may sign its assertion."));
        var assertion = await reader.HydrateAsync(new ManagementAssertion(request.TenantId, request.ProgramId,
            request.AssertionId), ct).ConfigureAwait(false);
        if (assertion.Current is not { } state || state.CurrentVersion is not { } authored || state.ExaminationId != request.ExaminationId)
            return Conflict("The assertion was not found in the requested examination.");
        ManagementAssertionSignatureView signature;
        if (state.Signature is { } prior)
        {
            signature = prior with { AssertionVersion = request.AssertionVersion, PackageId = request.PackageId,
                PackageVersion = request.PackageVersion, SignerMemberId = memberId };
        }
        else
        {
            var content = authored.Content;
            if (authored.Version != request.AssertionVersion || content.PackageId != request.PackageId ||
                content.PackageVersion != request.PackageVersion || content.ScopeKind != setup.ExaminationType)
                return Conflict("The signature must name the current assertion and its exact selected package.");
            var package = await ExaminationSignOffSourceValidator.ReadPackageAsync(packages, request.TenantId,
                request.ProgramId, request.PackageId, ct).ConfigureAwait(false);
            if (!package.IsSuccess)
                return Result.Failure(package.Error);
            var version = package.Value.View?.Versions.SingleOrDefault(item => item.PackageVersion == request.PackageVersion);
            var validation = version?.Validations.LastOrDefault();
            if (package.Value.View is not { } packageView || packageView.ExaminationId != request.ExaminationId ||
                packageView.PackageKind != setup.ExaminationType || version is null ||
                version.Status is not ("validated" or "validated_with_overrides" or "disclosure_approved" or "delivered") ||
                validation is not { Status: "passed" or "passed_with_overrides" } ||
                validation.PackageVersion != request.PackageVersion || validation.ManifestId != content.PackageManifestId ||
                !HashEquals(validation.ManifestSha256, content.PackageManifestSha256) ||
                version.Manifest.AuditPackageManifestId != content.PackageManifestId ||
                !HashEquals(version.Manifest.ManifestSha256, content.PackageManifestSha256) ||
                version.SourceSnapshotId != content.SourceSnapshotId ||
                !HashEquals(version.SourceSnapshotSha256, content.SourceSnapshotSha256))
                return Conflict("Signing requires the exact successfully validated package manifest.");
            if (setup.ExaminationType == "type_i")
            {
                var baseline = await baselines.ReadAsync(request.TenantId, request.ProgramId, request.ExaminationId, ct).ConfigureAwait(false);
                if (!baseline.Rules.BaselineHistory.Any(item => item.TypeIBaselineId == version.SourceSnapshotId &&
                        item.ExaminationEngagementId == request.ExaminationId && item.Status is "frozen" or "amended" &&
                        HashEquals(item.ContentSha256, version.SourceSnapshotSha256)) || version.SourceSnapshotKind != "type_i_baseline")
                    return Conflict("The package must contain this examination's exact frozen baseline version.");
            }
            else
            {
                if (version.SourcePeriodId == Uuid.Empty || version.SourceSnapshotKind != "type_ii_close_snapshot")
                    return Conflict("The package must identify an approved observation-period close.");
                var period = await periods.TryReadAsync(request.TenantId, request.ProgramId, version.SourcePeriodId, ct).ConfigureAwait(false);
                if (!period.IsSuccess)
                    return Result.Failure(period.Error);
                if (period.Value.View is not { Status: "closed" } closed || closed.ExaminationId != request.ExaminationId ||
                    !closed.CloseSnapshots.Any(item => item.CloseSnapshotId == version.SourceSnapshotId &&
                        HashEquals(item.ContentSha256, version.SourceSnapshotSha256)))
                    return Conflict("The package must contain this examination's exact approved close version.");
            }
            var now = clock.GetUtcNow();
            if (now < validation.UpdatedAt || now < authored.AuthoredAt)
                return Conflict("The signature cannot precede its assertion or successful package validation.");
            signature = new ManagementAssertionSignatureView(request.AssertionVersion, request.PackageId,
                request.PackageVersion, content.PackageManifestId, content.PackageManifestSha256,
                content.SourceSnapshotId, content.SourceSnapshotSha256, version.SourceSnapshotKind, setup.Revision,
                memberId, setup.CeoSignerMemberId == memberId ? "ceo" : "ciso", actor.Value.Display, now);
        }
        return await executor.ExecuteAsync(new ManagementAssertion(request.TenantId, request.ProgramId, request.AssertionId),
            aggregate => CommandFailureRequestAdapter.ToOutcome(aggregate.Sign(request.ExpectedRevision, signature)), context, ct).ConfigureAwait(false);
    }

    static bool HashEquals(string? left, string? right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
