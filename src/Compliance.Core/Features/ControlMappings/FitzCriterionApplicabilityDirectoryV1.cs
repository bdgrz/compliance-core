using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Projects every program's criterion applicability decisions for coverage reads.</summary>
sealed class FitzCriterionApplicabilityDirectoryV1(IKvClient client)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/criterion-applicability-directory-v1/projection", ProjectorName),
        ICriterionApplicabilityDirectoryReader, ICriterionApplicabilityDirectoryProjection
{
    public const string ProjectorName = "CriterionApplicabilityDirectoryV1";
    public const string SourceArea = "criterion-applicability";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), SourceArea)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var lineId = domainEvent switch
        {
            CriterionNotApplicableProposed proposed => CriterionApplicability.IdentityOf(proposed.TenantId,
                proposed.ProgramId, proposed.EditionId, proposed.CriterionIdentifier),
            CriterionApplicabilityReviewed reviewed => CriterionApplicability.IdentityOf(reviewed.TenantId,
                reviewed.ProgramId, reviewed.EditionId, reviewed.CriterionIdentifier),
            CriterionNotApplicableWithdrawn withdrawn => CriterionApplicability.IdentityOf(withdrawn.TenantId,
                withdrawn.ProgramId, withdrawn.EditionId, withdrawn.CriterionIdentifier),
            _ => throw new ArgumentException("The event is not a criterion applicability event.",
                nameof(domainEvent)),
        };
        var current = await ControlCoverageDirectorySchema.Decisions.GetAsync(Transaction,
            lineId, ct).ConfigureAwait(false);
        var next = CriterionApplicabilityReducer.Apply(current, domainEvent);
        if (next.Revision != (current?.Revision ?? 0) + 1)
            throw new InvalidOperationException(
                "A criterion applicability revision cannot project before its predecessor.");
        if (current is null)
            await ControlCoverageDirectorySchema.Decisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await ControlCoverageDirectorySchema.Decisions.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<CriterionApplicabilityView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlCoverageDirectorySchema.Decisions.QueryAsync(tx,
            ControlCoverageDirectorySchema.DecisionsByProgram.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
