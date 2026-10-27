using Bdgrz.Compliance.Features.Kernel;
using System.Globalization;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Creates verified archive bytes and durably publishes successful output before acknowledging start.</summary>
public sealed class StartAuditPackageBuildHandler(AuditPackageSourceVerifier verifier,
    AuditPackageArchiveBuilder builder, IAuditPackageBuildOutputs outputs, IImmutableContentStore content)
    : IRequestHandler<StartAuditPackageBuild, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<StartAuditPackageBuild> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.ExpectedRevision is not null ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != new AuditPackageManifest(tenant, command.PackageId).Stream ||
            command.Generation != command.ManifestVersion)
            return ReceiverAppend.Invalid("The build effect names another owner, generation or revision authority.");
        try
        {
            var verified = await verifier.BuildAsync(tenant, command.PackageId, command.BuildId, command.ManifestVersion,
                command.OperationId, command.Source, command.Intent, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<ReceiverOutcome>.Failure(verified.Error!);
            if (command.CommandId != EffectIdentity.CommandId(command.Source.EventId,
                    $"{AuditPackageBuildProcess.Name}:{nameof(StartAuditPackageBuild)}", command.Target.ToString(), verified.Value!.Intent.Header.Episode))
                return ReceiverAppend.Invalid("The receiver command id differs from its exact source effect key.");
            var existing = await outputs.ReadAsync(tenant, command.BuildId, ct).ConfigureAwait(false);
            if (existing is not null)
                return existing.Source == command.Source && existing.Intent == command.Intent
                    ? Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("verified_archive_recorded"))
                    : ReceiverAppend.Invalid("The logical build output belongs to another exact source or intent.");
            if (verified.Value!.Owner.IsFrozen(command.ManifestVersion))
                return ReceiverAppend.Superseded("The admitted build version was already frozen by a delivery.");
            var built = await builder.BuildAsync(tenant, verified.Value.Manifest, ct).ConfigureAwait(false);
            if (!built.IsSuccess)
                return Result<ReceiverOutcome>.Failure(built.Error!);
            var frozen = await content.WriteJsonAsync(tenant.ToString(),
                Uuid.CreateVersion5(command.BuildId, "verified-output:" + command.Source.EventId),
                "audit-package-build-output.v1", built.Value!, ct).ConfigureAwait(false);
            await outputs.PublishIfAbsentAsync(new AuditPackageBuildOutputRecord(tenant, command.BuildId,
                command.Source, command.Intent, frozen), ct).ConfigureAwait(false);
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("verified_archive_recorded"));
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return ReceiverAppend.Wait("The admitted build content is not available yet: " + exception.Message);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ReceiverAppend.Invalid("The admitted build content cannot be verified: " + exception.Message);
        }
    }
}
