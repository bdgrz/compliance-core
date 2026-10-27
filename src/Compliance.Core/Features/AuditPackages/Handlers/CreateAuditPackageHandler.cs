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

/// <summary>Creates a native manifest only from its exact frozen examination source.</summary>
public sealed class CreateAuditPackageHandler(AuditPackageRootOperation operations,
    AuditPackageSourceBuilder sources, IImmutableContentStore content) : IRequestHandler<CreateAuditPackage>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateAuditPackage> context, CancellationToken ct)
    {
        var request = context.Request;
        var outcome = await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PackageId,
            request.PackageId, nameof(CreateAuditPackage), null, _ => 1,
            owner => owner.PrepareCreate(request.ProgramId), async (_, operation, token) =>
            {
                var resolved = await sources.ResolveAsync(request.TenantId, request.ProgramId, request.Content, token)
                    .ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result<PreparedPackageDelta>.Failure(resolved.Error!);
                var rules = new AuditPackageRules(request.TenantId, request.ProgramId, request.PackageId);
                if (rules.Create(resolved.Value!, PackageChanges.Member(operation), operation.Actor.Display,
                        operation.AcceptedAt) is { } failure)
                    return PackageChanges.Failure(failure);
                return Result<PreparedPackageDelta>.Success(await PackageContent.DeltaAsync(content, request.TenantId,
                    "AuditPackageCreated.change.v1", PackageChanges.Version(rules.Current!, operation), token)
                    .ConfigureAwait(false));
            }, (owner, operation, delta, response, result) =>
                owner.Create(request.ProgramId, operation, delta.Content, response, result), false, ct).ConfigureAwait(false);
        return AuditPackageRootOperation.Unit(outcome);
    }
}
