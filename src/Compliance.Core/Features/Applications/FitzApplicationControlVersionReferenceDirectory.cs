using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationControlVersionReferenceDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-control-version-references-v1/projection",
            ProjectorName),
        IApplicationControlVersionReferenceDirectory,
        IApplicationControlVersionReferenceProjection
{
    public const string ProjectorName = "ApplicationControlVersionReferencesV1";
    const int MaximumApplicabilityEntries = 100;
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginVersionBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginVersionBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginVersionBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != "controls" ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "A control-version reference batch requires its tenant's control area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ControlDraftCreated created:
                EnsureTenant(created.TenantId);
                await ApplicationControlVersionReferenceSchema.States.InsertAsync(Transaction,
                    new ApplicationControlVersionReferenceState(created.TenantId,
                        created.ProgramId, created.ControlId, created.Identifier, null, null,
                        null, null, false, null), ct).ConfigureAwait(false);
                break;
            case ControlDraftDiscarded discarded:
                var discarding = await RequireStateAsync(discarded.TenantId,
                    discarded.ProgramId, discarded.ControlId, ct).ConfigureAwait(false);
                if (discarding.CurrentVersionId is not null)
                    throw new InvalidOperationException(
                        "An approved control cannot discard its version-reference state.");
                await ApplicationControlVersionReferenceSchema.States.DeleteAsync(Transaction,
                    discarded.ControlId, ct).ConfigureAwait(false);
                break;
            case ControlSuccessorProposed proposed:
                var proposing = await RequireStateAsync(proposed.TenantId, proposed.ProgramId,
                    proposed.ControlId, ct).ConfigureAwait(false);
                if (proposing.CurrentVersionId != proposed.PredecessorVersionId ||
                    proposing.PendingSuccessorVersionId is not null ||
                    proposing.PendingRetirementId is not null || proposing.IsRetired)
                    throw new InvalidOperationException(
                        "A control successor proposal does not match its current approved version.");
                await UpdateVersionAsync(proposed.ControlId, proposed.PredecessorVersionId,
                    reference => reference with { HasPendingSuccessor = true }, ct)
                    .ConfigureAwait(false);
                await ReplaceStateAsync(proposing, proposing with
                {
                    PendingSuccessorVersionId = proposed.VersionId,
                }, ct).ConfigureAwait(false);
                break;
            case ControlRetirementProposed proposed:
                var retiring = await RequireStateAsync(proposed.TenantId, proposed.ProgramId,
                    proposed.ControlId, ct).ConfigureAwait(false);
                if (retiring.CurrentVersionId != proposed.VersionId ||
                    retiring.PendingSuccessorVersionId is not null ||
                    retiring.PendingRetirementId is not null || retiring.IsRetired ||
                    retiring.CurrentEffectiveFrom is null ||
                    proposed.EffectiveUntil <= retiring.CurrentEffectiveFrom.Value)
                    throw new InvalidOperationException(
                        "A control retirement proposal does not match its current approved version.");
                await UpdateVersionAsync(proposed.ControlId, proposed.VersionId,
                    reference => reference with
                    {
                        PendingRetirementEffectiveUntil = proposed.EffectiveUntil,
                    }, ct).ConfigureAwait(false);
                await ReplaceStateAsync(retiring, retiring with
                {
                    PendingRetirementId = proposed.RetirementId,
                    PendingRetirementEffectiveUntil = proposed.EffectiveUntil,
                }, ct).ConfigureAwait(false);
                break;
            case ControlProposalWithdrawn withdrawn:
                await WithdrawAsync(withdrawn, ct).ConfigureAwait(false);
                break;
            case ControlApproved approved:
                await ApproveAsync(approved, ct).ConfigureAwait(false);
                break;
            case ControlRetired retired:
                await RetireAsync(retired, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    "The domain event is not supported by the control-version reference projection.");
        }
    }

    async ValueTask WithdrawAsync(ControlProposalWithdrawn withdrawn, CancellationToken ct)
    {
        var state = await RequireStateAsync(withdrawn.TenantId, withdrawn.ProgramId,
            withdrawn.ControlId, ct).ConfigureAwait(false);
        if (state.CurrentVersionId != withdrawn.CurrentVersionId)
            throw new InvalidOperationException(
                "A control proposal withdrawal does not match its current approved version.");

        switch (withdrawn.Kind)
        {
            case "successor" when state.PendingSuccessorVersionId == withdrawn.TargetId:
                await UpdateVersionAsync(withdrawn.ControlId, withdrawn.CurrentVersionId,
                    reference => reference with { HasPendingSuccessor = false }, ct)
                    .ConfigureAwait(false);
                await ReplaceStateAsync(state, state with
                {
                    PendingSuccessorVersionId = null,
                }, ct).ConfigureAwait(false);
                break;
            case "retirement" when state.PendingRetirementId == withdrawn.TargetId:
                await UpdateVersionAsync(withdrawn.ControlId, withdrawn.CurrentVersionId,
                    reference => reference with
                    {
                        PendingRetirementEffectiveUntil = null,
                    }, ct).ConfigureAwait(false);
                await ReplaceStateAsync(state, state with
                {
                    PendingRetirementId = null,
                    PendingRetirementEffectiveUntil = null,
                }, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    "A control proposal withdrawal does not match a pending proposal.");
        }
    }

    async ValueTask ApproveAsync(ControlApproved approved, CancellationToken ct)
    {
        var state = await RequireStateAsync(approved.TenantId, approved.ProgramId,
            approved.ControlId, ct).ConfigureAwait(false);
        if (approved.PredecessorVersionId is { } predecessorVersionId)
        {
            if (state.CurrentVersionId != predecessorVersionId ||
                state.PendingSuccessorVersionId != approved.VersionId ||
                state.PendingRetirementId is not null || state.IsRetired)
                throw new InvalidOperationException(
                    "A successor approval does not match its current control version.");
            await UpdateVersionAsync(approved.ControlId, predecessorVersionId,
                reference => reference with
                {
                    Status = "superseded",
                    EffectiveUntil = approved.EffectiveFrom,
                    HasPendingSuccessor = false,
                    PendingRetirementEffectiveUntil = null,
                }, ct).ConfigureAwait(false);
        }
        else if (state.CurrentVersionId is not null || state.IsRetired)
        {
            throw new InvalidOperationException(
                "An initial control approval cannot replace an existing approved version.");
        }

        await InsertApprovedReferencesAsync(approved, state.Identifier, ct)
            .ConfigureAwait(false);
        await ReplaceStateAsync(state, state with
        {
            CurrentVersionId = approved.VersionId,
            PendingSuccessorVersionId = null,
            PendingRetirementId = null,
            PendingRetirementEffectiveUntil = null,
            IsRetired = false,
            CurrentEffectiveFrom = approved.EffectiveFrom,
        }, ct).ConfigureAwait(false);
    }

    async ValueTask RetireAsync(ControlRetired retired, CancellationToken ct)
    {
        var state = await RequireStateAsync(retired.TenantId, retired.ProgramId,
            retired.ControlId, ct).ConfigureAwait(false);
        if (state.CurrentVersionId != retired.VersionId ||
            state.PendingRetirementId != retired.RetirementId || state.IsRetired ||
            state.PendingRetirementEffectiveUntil != retired.EffectiveUntil)
            throw new InvalidOperationException(
                "A control retirement does not match its current pending retirement.");
        await UpdateVersionAsync(retired.ControlId, retired.VersionId,
            reference => reference with
            {
                Status = "retired",
                EffectiveUntil = retired.EffectiveUntil,
                PendingRetirementEffectiveUntil = null,
            }, ct).ConfigureAwait(false);
        await ReplaceStateAsync(state, state with
        {
            PendingRetirementId = null,
            PendingRetirementEffectiveUntil = null,
            IsRetired = true,
        }, ct).ConfigureAwait(false);
    }

    async ValueTask InsertApprovedReferencesAsync(ControlApproved approved, string identifier,
        CancellationToken ct)
    {
        if (approved.VersionId == Uuid.Empty || approved.Revision < 1 ||
            approved.EffectiveFrom == default)
            throw new InvalidOperationException(
                "An approved control version has invalid version, revision, or effective date data.");
        var references = (approved.Content.Applicability ?? []).Where(IsIndexed).ToArray();
        if (references.Length > MaximumApplicabilityEntries)
            throw new InvalidOperationException(
                "An approved control version exceeds its applicability-reference bound.");

        foreach (var reference in references)
            await ApplicationControlVersionReferenceSchema.References.InsertAsync(Transaction,
                new ApplicationControlVersionReferenceView(approved.TenantId,
                    reference.SubjectType, reference.GovernedRecordId!.Value,
                    approved.ProgramId, approved.ControlId, identifier, approved.VersionId,
                    approved.Revision, "approved", approved.EffectiveFrom, null,
                    reference.EntryId, reference.Subject, reference.Rationale, false, null), ct)
                .ConfigureAwait(false);
    }

    async ValueTask UpdateVersionAsync(Uuid controlId, Uuid versionId,
        Func<ApplicationControlVersionReferenceView, ApplicationControlVersionReferenceView> update,
        CancellationToken ct)
    {
        var page = await ApplicationControlVersionReferenceSchema.References.QueryAsync(
            Transaction, ApplicationControlVersionReferenceSchema.ByControlVersion.Query()
                .WithPrefix(controlId.ToString(), versionId.ToString())
                .Take(MaximumApplicabilityEntries), ct).ConfigureAwait(false);
        if (page.NextCursor is not null)
            throw new InvalidOperationException(
                "A control version exceeds its applicability-reference bound.");
        foreach (var reference in page.Items)
            await ApplicationControlVersionReferenceSchema.References.ReplaceAsync(Transaction,
                reference, update(reference), ct).ConfigureAwait(false);
    }

    async ValueTask<ApplicationControlVersionReferenceState> RequireStateAsync(Uuid tenantId,
        Uuid programId, Uuid controlId, CancellationToken ct)
    {
        EnsureTenant(tenantId);
        var state = await ApplicationControlVersionReferenceSchema.States.GetAsync(Transaction,
            controlId, ct).ConfigureAwait(false);
        return state is { } found && found.TenantId == tenantId && found.ProgramId == programId &&
               found.ControlId == controlId
            ? found
            : throw new InvalidOperationException(
                "A control-version reference event has no matching tenant state.");
    }

    ValueTask ReplaceStateAsync(ApplicationControlVersionReferenceState current,
        ApplicationControlVersionReferenceState replacement, CancellationToken ct) =>
        ApplicationControlVersionReferenceSchema.States.ReplaceAsync(Transaction, current,
            replacement, ct);

    void EnsureTenant(Uuid tenantId)
    {
        if (_batchTenantId != tenantId)
            throw new InvalidOperationException(
                "A control-version reference event must belong to its tenant projection batch.");
    }

    static bool IsIndexed(ControlApplicabilityReference? reference) =>
        reference is { Unresolved: false, GovernedRecordId: { } recordId } &&
        recordId != Uuid.Empty && reference.EntryId != Uuid.Empty &&
        reference.SubjectType is "application" or "system_instance";

    public ValueTask<Page<ApplicationControlVersionReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default) => ListCoreAsync(tenantId, subjectType, recordId, limit,
        cursor, ct);

    async ValueTask<Page<ApplicationControlVersionReferenceView>> ListCoreAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor, CancellationToken ct)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationControlVersionReferenceSchema.References.QueryAsync(tx,
            ApplicationControlVersionReferenceSchema.ByRecord.Query()
                .WithPrefix(subjectType, recordId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls")), ct);
}
