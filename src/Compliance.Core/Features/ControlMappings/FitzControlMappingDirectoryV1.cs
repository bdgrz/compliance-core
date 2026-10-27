using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Projections;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Projects every program's mapping views for coverage and list reads.</summary>
sealed class FitzControlMappingDirectoryV1(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/control-mapping-directory-v1/projection",
            ProjectorName),
        IControlMappingDirectoryReader, IControlMappingDirectoryProjection
{
    public const string ProjectorName = "ControlMappingDirectoryV1";
    public const string SourceArea = "control-criterion-mappings";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), SourceArea)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var lineId = domainEvent switch
        {
            ControlCriterionMappingProposed proposed => ControlCriterionMapping.IdentityOf(proposed.TenantId,
                proposed.ProgramId, proposed.ControlId, proposed.EditionId, proposed.CriterionIdentifier),
            ControlCriterionMappingReviewed reviewed => ControlCriterionMapping.IdentityOf(reviewed.TenantId,
                reviewed.ProgramId, reviewed.ControlId, reviewed.EditionId, reviewed.CriterionIdentifier),
            ControlCriterionMappingRetired retired => ControlCriterionMapping.IdentityOf(retired.TenantId,
                retired.ProgramId, retired.ControlId, retired.EditionId, retired.CriterionIdentifier),
            _ => throw new ArgumentException("The event is not a control mapping event.",
                nameof(domainEvent)),
        };
        var current = await ControlCoverageDirectorySchema.Mappings.GetAsync(Transaction,
            lineId, ct).ConfigureAwait(false);
        _ = MappingLineTransitions.Next(MappingLineContent.Owner(domainEvent),
            current is null ? new MappingLineHeader(0, 0, Uuid.Empty, null, null, false) : MappingLineContent.Header(current), domainEvent);
        var decoded = await MappingLineContent.ReadAsync(content, domainEvent, ct).ConfigureAwait(false);
        var next = ControlCriterionMappingReducer.Apply(current, decoded.Change);
        MappingLineContent.ValidateCurrent(decoded.Current, MappingLineContent.Current(next));
        if (((IMappingLineFact)domainEvent).Header != MappingLineContent.Header(next))
            throw new ImmutableContentIntegrityException("The projected current authority has another native header.");
        if (next.Revision != (current?.Revision ?? 0) + 1)
            throw new InvalidOperationException(
                "A control mapping revision cannot project before its predecessor.");
        if (current is null)
            await ControlCoverageDirectorySchema.Mappings.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await ControlCoverageDirectorySchema.Mappings.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ControlCriterionMappingView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlCoverageDirectorySchema.Mappings.QueryAsync(tx,
            ControlCoverageDirectorySchema.MappingsByProgram.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
