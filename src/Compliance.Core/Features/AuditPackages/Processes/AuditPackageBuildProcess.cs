using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.AuditPackages.Processes;

/// <summary>Technical ordered build stages; retries and failures never become manifest business facts.</summary>
public static class AuditPackageBuildProcess
{
    public const string Name = "AuditPackageBuildProcess";
    public static ProcessDefinition Definition() => new(Name, [nameof(AuditPackageBuildRequested)], true,
        [new ProcessStageDefinition("start", [nameof(StartAuditPackageBuild)], PerTarget: true),
            new ProcessStageDefinition("record", [nameof(RecordAuditPackageBuildResult)], PerTarget: true)], [],
        CreateAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var tenant = Uuid.Parse(step.Record.Key.Realm, CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        var intent = await PackageContent.ReadAsync<PackageBuildIntent>(content, tenant, step.Record.Intent, ct)
            .ConfigureAwait(false);
        var target = new AuditPackageManifest(tenant, intent.PackageId).Stream;
        if (step.Target != target.ToString())
            throw new ImmutableContentIntegrityException("The build process target differs from its canonical owner.");
        if (step.Receiver == nameof(StartAuditPackageBuild))
            return new StartAuditPackageBuild(step.CommandId, step.Record.Source, target,
                step.Record.Key.RootOperationId, intent.PackageVersion, null, step.Record.Intent,
                intent.PackageId, intent.BuildId, intent.PackageVersion);
        if (step.Receiver != nameof(RecordAuditPackageBuildResult))
            return null;
        var checkpoint = await services.GetRequiredService<IAuditPackageBuildOutputs>().ReadAsync(tenant,
            intent.BuildId, ct).ConfigureAwait(false);
        if (checkpoint is null)
            throw new SourceFactVerificationException("The successful build checkpoint is not visible yet.");
        if (checkpoint.Source != step.Record.Source || checkpoint.Intent != step.Record.Intent)
            throw new ImmutableContentIntegrityException("The build checkpoint belongs to another admitted source.");
        var output = await PackageContent.ReadAsync<AuditPackageBuildOutput>(content, tenant,
            checkpoint.VerifiedOutput, ct).ConfigureAwait(false);
        return new RecordAuditPackageBuildResult(step.CommandId, step.Record.Source, target,
            step.Record.Key.RootOperationId, intent.PackageVersion, null, step.Record.Intent,
            intent.PackageId, intent.BuildId, output);
    }
}
