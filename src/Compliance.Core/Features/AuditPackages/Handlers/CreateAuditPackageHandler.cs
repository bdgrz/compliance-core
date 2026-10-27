using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
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
