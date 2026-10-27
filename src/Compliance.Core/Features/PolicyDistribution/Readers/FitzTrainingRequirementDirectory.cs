using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Readers;

/// <summary>The program catalog of training requirements: a projection of the requirement owners' latest versions.</summary>
sealed class FitzTrainingRequirementDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/training-requirement-directory/projection-v1", ProjectorName),
        ITrainingRequirementDirectoryReader, ITrainingRequirementDirectoryProjection
{
    public const string ProjectorName = "TrainingRequirementDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), TrainingRequirement.Area)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not TrainingRequirementDefined defined)
            return;
        var view = await TrainingRequirementRules.ViewAsync(content, defined, defined.Version, ct).ConfigureAwait(false);
        var current = await TrainingRequirementDirectorySchema.Requirements.GetAsync(Transaction,
            defined.RequirementId, ct).ConfigureAwait(false);
        if (current is null)
            await TrainingRequirementDirectorySchema.Requirements.InsertAsync(Transaction, view, ct)
                .ConfigureAwait(false);
        else if (current.Version < view.Version)
            await TrainingRequirementDirectorySchema.Requirements.ReplaceAsync(Transaction, current, view, ct)
                .ConfigureAwait(false);
    }

    public async ValueTask<Page<TrainingRequirementView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TrainingRequirementDirectorySchema.Requirements.QueryAsync(tx,
            TrainingRequirementDirectorySchema.ByProgramIdentifier.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
