using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AuditPackages.Processes;

/// <summary>Independent historical delivery materialization with no external send or abort stage.</summary>
public static class AuditPackageDeliveryRecordingProcess
{
    public const string Name = "AuditPackageDeliveryRecordingProcess";
    public static ProcessDefinition Definition() => new(Name, [nameof(AuditPackageDeliveryAdmitted)], false,
        [new ProcessStageDefinition("materialize", [nameof(MaterializeRecordedAuditPackageDelivery)], PerTarget: true)],
        [], CreateAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        if (step.Receiver != nameof(MaterializeRecordedAuditPackageDelivery))
            return null;
        var tenant = Uuid.Parse(step.Record.Key.Realm, CultureInfo.InvariantCulture);
        var intent = await PackageContent.ReadAsync<PackageDeliveryRecordingIntent>(
            services.GetRequiredService<IImmutableContentStore>(), tenant, step.Record.Intent, ct).ConfigureAwait(false);
        var target = new AuditPackageDelivery(tenant, intent.PackageId, intent.PackageVersion, intent.DeliveryId).Stream;
        if (step.Target != target.ToString())
            throw new ImmutableContentIntegrityException("The delivery process target differs from its canonical child.");
        return new MaterializeRecordedAuditPackageDelivery(step.CommandId, step.Record.Source,
            new AuditPackageDelivery(tenant, intent.PackageId, intent.PackageVersion, intent.DeliveryId).Stream,
            step.Record.Key.RootOperationId, intent.PackageVersion, null, step.Record.Intent,
            intent.PackageId, intent.DeliveryId, intent.PackageVersion);
    }
}
