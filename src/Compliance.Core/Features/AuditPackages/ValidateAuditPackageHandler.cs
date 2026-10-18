using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Validates that the current package selection still matches its frozen source snapshot.</summary>
public sealed class ValidateAuditPackageHandler(IAggregateExecutor executor, IAggregateReader reader,
    AuditPackageSourceBuilder sources, TimeProvider clock) : IRequestHandler<ValidateAuditPackage>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ValidateAuditPackage> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPackageCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Audit package validation requires a personal HTTP actor."));
        if (request.PackageId == Uuid.Empty || request.PackageVersion is < 1 or > int.MaxValue ||
            string.IsNullOrWhiteSpace(request.ValidationKey))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Validation requires a package, positive version, and validation key."));

        var aggregateId = new AuditPackage(request.TenantId, request.ProgramId, request.PackageId);
        var loaded = await reader.HydrateAsync(aggregateId, ct).ConfigureAwait(false);
        if (loaded.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The audit package was not found in this program."));
        if (current.CurrentVersion != request.PackageVersion)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Validation must target the current package version."));

        var version = current.Versions[^1];
        var content = new AuditPackageContent(current.ExaminationId, current.PackageKind,
            version.SourceSnapshotKind, version.SourceSnapshotId, version.SourceSnapshotSha256,
            current.Name, version.SelectedContent, version.SourcePeriodId);
        var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId, content, ct)
            .ConfigureAwait(false);
        IReadOnlyList<AuditPackageValidationFinding> findings = resolved.IsSuccess
            ? Array.Empty<AuditPackageValidationFinding>()
            : [new AuditPackageValidationFinding(
                Uuid.CreateVersion5(request.PackageId,
                    $"{request.PackageVersion}\n{request.ValidationKey}\nfrozen-source"),
                "frozen_source_mismatch", "error", true, false,
                "Selected source versions do not match a supported frozen examination snapshot.")];

        return await executor.ExecuteAsync(aggregateId, aggregate =>
            CommandFailureRequestAdapter.ToOutcome(aggregate.RecordValidation(aggregate.Revision,
                (int)request.PackageVersion, request.ValidationKey, findings, actorMemberId, actorDisplay,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
