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

/// <summary>Records only a durable successful output whose actual ZIP verifies against the original admission.</summary>
public sealed class RecordAuditPackageBuildResultHandler(AuditPackageSourceVerifier verifier,
    IAggregateReader reader, ReceiverAppend append, IAuditPackageBuildOutputs outputs, IImmutableContentStore content,
    IArtifactContentStore artifacts, TimeProvider clock) : IRequestHandler<RecordAuditPackageBuildResult, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RecordAuditPackageBuildResult> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.ExpectedRevision is < 0 ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Target != new AuditPackageManifest(tenant, command.PackageId).Stream)
            return ReceiverAppend.Invalid("The build callback names another owner or invalid revision.");
        try
        {
            var verified = await verifier.BuildAsync(tenant, command.PackageId, command.BuildId, command.Generation,
                command.OperationId, command.Source, command.Intent, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<ReceiverOutcome>.Failure(verified.Error!);
            if (command.CommandId != EffectIdentity.CommandId(command.Source.EventId,
                    $"{AuditPackageBuildProcess.Name}:{nameof(RecordAuditPackageBuildResult)}", command.Target.ToString(), verified.Value!.Intent.Header.Episode))
                return ReceiverAppend.Invalid("The receiver command id differs from its exact source effect key.");
            var operation = SourceUseReceiverSupport.Child(command.CommandId, nameof(RecordAuditPackageBuildResult),
                command.Source.EventId, verified.Value!.Intent.Header, command.Target, clock.GetUtcNow());
            var binding = AcceptedOperationBinding.Of(operation);
            var selected = await reader.HydrateAsync(new AuditPackageManifest(tenant, command.PackageId,
                command.CommandId, nameof(RecordAuditPackageBuildResult)), ct).ConfigureAwait(false);
            if (selected.Acceptance.Decide(binding) == NativeAcceptanceDecision.Conflict)
                return ReceiverAppend.Invalid("The callback command was already accepted with another intent.");
            if (selected.Acceptance.Decide(binding) == NativeAcceptanceDecision.Replayed)
                return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(
                    NativeFactReference.Of(selected.Stream, selected.Acceptance.SelectedFact!)));
            if (selected.IsFrozen(command.Generation))
                return ReceiverAppend.Superseded("The exact build version has already been frozen by a delivery.");
            var checkpoint = await outputs.ReadAsync(tenant, command.BuildId, ct).ConfigureAwait(false);
            if (checkpoint is null)
                return ReceiverAppend.Wait("The verified successful build checkpoint is not visible yet.");
            if (checkpoint.Source != command.Source || checkpoint.Intent != command.Intent)
                return ReceiverAppend.Invalid("The output checkpoint belongs to another build admission.");
            var output = await PackageContent.ReadAsync<AuditPackageBuildOutput>(content, tenant,
                checkpoint.VerifiedOutput, ct).ConfigureAwait(false);
            if (!Matches(command.Output, output) || output.FailureCode is not null ||
                output.ManifestId != verified.Value.Manifest.AuditPackageManifestId ||
                output.ManifestSha256 != verified.Value.Manifest.ManifestSha256 ||
                output.IncludedEntries is null || !output.IncludedEntries.SequenceEqual(verified.Value.Manifest.Entries) ||
                output.ArchiveArtifactId is null || output.ArchiveSha256 is null || output.ArchiveBytes is not > 0)
                return ReceiverAppend.Invalid("The callback differs from the exact complete verified successful output.");
            var build = new AuditPackageBuildAttemptView(command.BuildId, verified.Value.Intent.BuildKey, "built",
                output.IncludedEntries.Count, output.IncludedEntries.Count, operation.AcceptedAt, operation.AcceptedAt,
                null, string.Empty, operation.AcceptedAt, null, null, output.ArchiveArtifactId, output.ArchiveSha256,
                output.ArchiveBytes);
            var failure = await AuditPackageArchiveVerifier.FailureCodeAsync(artifacts, tenant, build,
                verified.Value.Manifest, ct).ConfigureAwait(false);
            if (failure is not null)
                return failure == "archive_content_unavailable" ? ReceiverAppend.Wait("The actual archive is unavailable.")
                    : ReceiverAppend.Invalid("The actual archive does not match the frozen manifest.");
            var change = await PackageContent.WriteAsync(content, tenant, "AuditPackageBuilt.change.v1",
                new AuditPackageBuiltChange(command.BuildId, output, operation.AcceptedAt), ct).ConfigureAwait(false);
            var terminalResult = await PackageContent.FreezeUnitResultAsync(content, tenant, ct).ConfigureAwait(false);
            return await append.ApplyAsync(() => new AuditPackageManifest(tenant, command.PackageId,
                    command.CommandId, nameof(RecordAuditPackageBuildResult)), owner => owner.Acceptance, binding,
                owner => owner.IsFrozen(command.Generation)
                    ? ReceiverAppend.Superseded("The exact version was frozen by a delivery.") : null,
                owner => owner.RecordBuilt(verified.Value.Admission.ProgramId, command.ExpectedRevision ?? owner.Revision,
                    command.Generation, operation, command.Source, change, terminalResult), context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return ReceiverAppend.Wait("The successful output content is not available yet: " + exception.Message);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ReceiverAppend.Invalid("The successful output content cannot be verified: " + exception.Message);
        }
    }

    static bool Matches(AuditPackageBuildOutput left, AuditPackageBuildOutput right) =>
        left.ManifestId == right.ManifestId && left.ManifestSha256 == right.ManifestSha256 &&
        left.ArchiveArtifactId == right.ArchiveArtifactId && left.ArchiveSha256 == right.ArchiveSha256 &&
        left.ArchiveBytes == right.ArchiveBytes && left.FailureCode == right.FailureCode &&
        left.IncludedEntries is not null && right.IncludedEntries is not null &&
        left.IncludedEntries.SequenceEqual(right.IncludedEntries);
}
