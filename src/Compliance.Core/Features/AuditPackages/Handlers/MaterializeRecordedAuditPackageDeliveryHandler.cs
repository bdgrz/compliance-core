using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.History;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

/// <summary>Materializes the exact admitted personal handoff even after a later manifest version exists.</summary>
public sealed class MaterializeRecordedAuditPackageDeliveryHandler(AuditPackageSourceVerifier verifier,
    IAggregateReader reader, ReceiverAppend append, IImmutableContentStore content, TimeProvider clock, IDomainEventReader events)
    : IRequestHandler<MaterializeRecordedAuditPackageDelivery, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeRecordedAuditPackageDelivery> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.ExpectedRevision is not (null or 0) ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant) ||
            command.Generation != command.ManifestVersion || command.Target != new AuditPackageDelivery(tenant,
                command.PackageId, command.ManifestVersion, command.DeliveryId).Stream)
            return ReceiverAppend.Invalid("The historical handoff names another child, generation or revision.");
        try
        {
            var verified = await verifier.DeliveryAsync(tenant, command.PackageId, command.DeliveryId,
                command.ManifestVersion, command.OperationId, command.Source, command.Intent, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<ReceiverOutcome>.Failure(verified.Error!);
            if (command.CommandId != EffectIdentity.CommandId(command.Source.EventId,
                    $"{AuditPackageDeliveryRecordingProcess.Name}:{nameof(MaterializeRecordedAuditPackageDelivery)}", command.Target.ToString(), verified.Value!.Header.Episode))
                return ReceiverAppend.Invalid("The receiver command id differs from its exact source effect key.");
            var parent = await reader.HydrateAsync(new AuditPackageManifest(tenant, command.PackageId), ct)
                .ConfigureAwait(false);
            var history = await AuditPackageManifestHistory.ReadAsync(events, parent, ct).ConfigureAwait(false);
            var admitted = (AuditPackageDeliveryAdmitted)history.FactById(command.Source.EventId)!;
            var operation = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeRecordedAuditPackageDelivery),
                command.Source.EventId, verified.Value!.Header, command.Target, clock.GetUtcNow());
            var terminalResult = await PackageContent.FreezeUnitResultAsync(content, tenant, ct).ConfigureAwait(false);
            return await append.ApplyAsync(() => new AuditPackageDelivery(tenant, command.PackageId,
                    command.ManifestVersion, command.DeliveryId, command.CommandId), owner => owner.Acceptance,
                AcceptedOperationBinding.Of(operation), owner => owner.IsMaterialized
                    ? ReceiverAppend.Invalid("The handoff child was materialized by another logical effect.") : null,
                owner => owner.Materialize(admitted.ProgramId, operation, verified.Value!, command.Intent,
                    command.Source, terminalResult), context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return ReceiverAppend.Wait("The admitted handoff content is not available yet: " + exception.Message);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ReceiverAppend.Invalid("The admitted handoff content cannot be verified: " + exception.Message);
        }
    }
}
