using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Readers;

sealed class FitzApplicationPolicyReferenceDirectory(IKvClient client, IImmutableContentStore contentStore)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-policy-references-v2/projection", ProjectorName),
        IApplicationPolicyReferenceDirectory, IApplicationPolicyReferenceProjection
{
    public const string ProjectorName = "ApplicationPolicyReferencesV2";
    const int MaximumApplicabilityEntries = 100;
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginPolicyBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginPolicyBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginPolicyBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != "policies" ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "An application policy-reference batch requires its tenant's policy area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not IPolicyFact fact)
            throw new InvalidOperationException("A policy reference projection requires a native Policy fact.");
        var mutation = await PolicyFactContent.ReadChangeAsync(contentStore, fact, ct).ConfigureAwait(false);
        switch (mutation.Change)
        {
            case PolicyDraftCreatedContent created:
                await CreateAsync(created, ct).ConfigureAwait(false);
                break;
            case PolicyDraftRevisedContent revised:
                await ReviseAsync(revised, ct).ConfigureAwait(false);
                break;
            case PolicyReviewedContent reviewed:
                await ReviewAsync(reviewed, ct).ConfigureAwait(false);
                break;
            case PolicyApprovedContent approved:
                await ApproveAsync(approved, ct).ConfigureAwait(false);
                break;
            case PolicyRetirementProposedContent proposed:
                await ProposeRetirementAsync(proposed, ct).ConfigureAwait(false);
                break;
            case PolicyRetiredContent retired:
                await RetireAsync(retired, ct).ConfigureAwait(false);
                break;
            case PolicyDraftDiscardedContent discarded:
                await DiscardAsync(discarded, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    "The domain event is not supported by the application policy-reference projection.");
        }
    }

    async ValueTask CreateAsync(PolicyDraftCreatedContent created, CancellationToken ct)
    {
        EnsureTenant(created.TenantId);
        var content = ReferenceContent(created.Content);
        var state = new ApplicationPolicyReferenceState(created.TenantId, created.ProgramId,
            created.PolicyId, created.Identifier, 1, content, created.ContentSha256, null,
            null, null, null, null, false, 0,
            CountUnresolved(content));
        await ApplicationPolicyReferenceSchema.States.InsertAsync(Transaction, state, ct)
            .ConfigureAwait(false);
        await InsertReferencesAsync(state, null, "draft", "draft", 1, null, null,
            hasPendingSuccessor: false, ct).ConfigureAwait(false);
        await UpdateCoverageAsync(created.TenantId, 0, state.UnresolvedReferenceCount, ct)
            .ConfigureAwait(false);
    }

    async ValueTask ReviseAsync(PolicyDraftRevisedContent revised, CancellationToken ct)
    {
        EnsureTenant(revised.TenantId);
        var state = await RequireStateAsync(revised.TenantId, revised.ProgramId,
            revised.PolicyId, ct).ConfigureAwait(false);
        if (revised.Revision != state.Revision + 1 || state.Retired ||
            revised.PredecessorVersion is { } announcedPredecessor &&
            announcedPredecessor != state.CurrentVersion ||
            state.DraftContent is null && revised.PredecessorVersion is null &&
            state.CurrentVersion is not null)
            throw new InvalidOperationException(
                "A policy draft revision does not match its projected policy state.");

        var predecessorVersion = state.DraftContent is null
            ? revised.PredecessorVersion
            : state.DraftPredecessorVersion;
        if (predecessorVersion is not null && predecessorVersion != state.CurrentVersion)
            throw new InvalidOperationException(
                "A policy successor draft does not match the current approved version.");

        await DeleteReferencesAsync(revised.PolicyId, "draft", ct).ConfigureAwait(false);
        var content = ReferenceContent(revised.Content);
        var replacement = state with
        {
            Revision = revised.Revision,
            DraftContent = content,
            DraftContentSha256 = revised.ContentSha256,
            DraftPredecessorVersion = predecessorVersion,
            UnresolvedReferenceCount = CountUnresolved(state.CurrentContent, content,
                state.HistoricalUnresolvedReferenceCount),
        };
        await ReplaceStateAsync(state, replacement, ct).ConfigureAwait(false);
        await InsertReferencesAsync(replacement, null, "draft", "draft", revised.Revision,
            null, null, hasPendingSuccessor: false, ct).ConfigureAwait(false);
        if (predecessorVersion is { } currentVersion)
            await UpdateVersionReferencesAsync(revised.PolicyId,
                currentVersion.ToString(CultureInfo.InvariantCulture),
                reference => reference with { HasPendingSuccessor = true }, ct)
                .ConfigureAwait(false);
    }

    async ValueTask ReviewAsync(PolicyReviewedContent reviewed, CancellationToken ct)
    {
        EnsureTenant(reviewed.TenantId);
        var state = await RequireStateAsync(reviewed.TenantId, reviewed.ProgramId,
            reviewed.PolicyId, ct).ConfigureAwait(false);
        if (reviewed.Revision != state.Revision)
            throw new InvalidOperationException(
                "A policy review does not match its projected policy revision.");
        if (reviewed.Outcome == "accept" || state.DraftContent is not null ||
            state.PendingRetirementEffectiveUntil is null)
            return;

        var replacement = state with { PendingRetirementEffectiveUntil = null };
        await ReplaceStateAsync(state, replacement, ct).ConfigureAwait(false);
        if (state.CurrentVersion is { } version)
            await UpdateVersionReferencesAsync(reviewed.PolicyId,
                version.ToString(CultureInfo.InvariantCulture),
                reference => reference with { PendingRetirementEffectiveUntil = null }, ct)
                .ConfigureAwait(false);
    }

    async ValueTask ApproveAsync(PolicyApprovedContent approved, CancellationToken ct)
    {
        EnsureTenant(approved.TenantId);
        var state = await RequireStateAsync(approved.TenantId, approved.ProgramId,
            approved.PolicyId, ct).ConfigureAwait(false);
        if (approved.Revision != state.Revision || state.DraftContent is null ||
            state.DraftContentSha256 != approved.ContentSha256 ||
            approved.Version != (state.CurrentVersion ?? 0) + 1 ||
            approved.PredecessorVersion != state.DraftPredecessorVersion ||
            (approved.PredecessorVersion is { } predecessor &&
             (state.CurrentVersion != predecessor || state.CurrentEffectiveFrom is null ||
              approved.EffectiveFrom <= state.CurrentEffectiveFrom.Value)) ||
            (approved.PredecessorVersion is null && state.CurrentVersion is not null))
            throw new InvalidOperationException(
                "A policy approval does not match its projected draft and version state.");

        if (state.CurrentVersion is { } currentVersion)
        {
            await UpdateVersionReferencesAsync(approved.PolicyId,
                currentVersion.ToString(CultureInfo.InvariantCulture),
                reference => reference with
                {
                    Status = "superseded",
                    EffectiveUntil = approved.EffectiveFrom,
                    HasPendingSuccessor = false,
                    PendingRetirementEffectiveUntil = null,
                }, ct).ConfigureAwait(false);
        }
        await DeleteReferencesAsync(approved.PolicyId, "draft", ct).ConfigureAwait(false);
        var historicalUnresolved = state.HistoricalUnresolvedReferenceCount +
            (state.CurrentVersion is null || state.CurrentContent is null
                ? 0
                : CountUnresolved(state.CurrentContent));
        var replacement = state with
        {
            DraftContent = null,
            DraftContentSha256 = null,
            DraftPredecessorVersion = null,
            CurrentContent = state.DraftContent,
            CurrentVersion = approved.Version,
            CurrentEffectiveFrom = approved.EffectiveFrom,
            PendingRetirementEffectiveUntil = null,
            Retired = false,
            HistoricalUnresolvedReferenceCount = historicalUnresolved,
            UnresolvedReferenceCount = historicalUnresolved +
                CountUnresolved(state.DraftContent),
        };
        await ReplaceStateAsync(state, replacement, ct).ConfigureAwait(false);
        await InsertReferencesAsync(replacement, approved.Version,
            approved.Version.ToString(CultureInfo.InvariantCulture),
            "approved", approved.Revision, approved.EffectiveFrom, null,
            hasPendingSuccessor: false, ct).ConfigureAwait(false);
    }

    async ValueTask ProposeRetirementAsync(PolicyRetirementProposedContent proposed,
        CancellationToken ct)
    {
        EnsureTenant(proposed.TenantId);
        var state = await RequireStateAsync(proposed.TenantId, proposed.ProgramId,
            proposed.PolicyId, ct).ConfigureAwait(false);
        if (proposed.Revision != state.Revision + 1 || state.DraftContent is not null ||
            state.CurrentVersion != proposed.Version || state.CurrentEffectiveFrom is null ||
            proposed.EffectiveUntil <= state.CurrentEffectiveFrom.Value || state.Retired ||
            state.PendingRetirementEffectiveUntil is not null)
            throw new InvalidOperationException(
                "A policy retirement proposal does not match its current approved version.");
        var replacement = state with
        {
            Revision = proposed.Revision,
            PendingRetirementEffectiveUntil = proposed.EffectiveUntil,
        };
        await ReplaceStateAsync(state, replacement, ct).ConfigureAwait(false);
        await UpdateVersionReferencesAsync(proposed.PolicyId,
            proposed.Version.ToString(CultureInfo.InvariantCulture),
            reference => reference with
            {
                PendingRetirementEffectiveUntil = proposed.EffectiveUntil,
            }, ct).ConfigureAwait(false);
    }

    async ValueTask RetireAsync(PolicyRetiredContent retired, CancellationToken ct)
    {
        EnsureTenant(retired.TenantId);
        var state = await RequireStateAsync(retired.TenantId, retired.ProgramId,
            retired.PolicyId, ct).ConfigureAwait(false);
        if (retired.Revision != state.Revision || state.CurrentVersion != retired.Version ||
            state.PendingRetirementEffectiveUntil != retired.EffectiveUntil || state.Retired)
            throw new InvalidOperationException(
                "A policy retirement does not match its projected proposal and version state.");
        var replacement = state with
        {
            PendingRetirementEffectiveUntil = null,
            Retired = true,
        };
        await ReplaceStateAsync(state, replacement, ct).ConfigureAwait(false);
        await UpdateVersionReferencesAsync(retired.PolicyId,
            retired.Version.ToString(CultureInfo.InvariantCulture),
            reference => reference with
            {
                Status = "retired",
                EffectiveUntil = retired.EffectiveUntil,
                PendingRetirementEffectiveUntil = null,
            }, ct).ConfigureAwait(false);
    }

    async ValueTask DiscardAsync(PolicyDraftDiscardedContent discarded, CancellationToken ct)
    {
        EnsureTenant(discarded.TenantId);
        var state = await RequireStateAsync(discarded.TenantId, discarded.ProgramId,
            discarded.PolicyId, ct).ConfigureAwait(false);
        if (discarded.Revision != state.Revision || state.CurrentVersion is not null ||
            state.DraftContent is null)
            throw new InvalidOperationException(
                "A policy draft discard does not match its current unused draft state.");
        await DeleteReferencesAsync(discarded.PolicyId, "draft", ct).ConfigureAwait(false);
        await ApplicationPolicyReferenceSchema.States.DeleteAsync(Transaction,
            discarded.PolicyId, ct).ConfigureAwait(false);
        await UpdateCoverageAsync(discarded.TenantId, state.UnresolvedReferenceCount, 0, ct)
            .ConfigureAwait(false);
    }

    async ValueTask UpdateVersionReferencesAsync(Uuid policyId, string versionKey,
        Func<ApplicationPolicyReferenceView, ApplicationPolicyReferenceView> update,
        CancellationToken ct)
    {
        var page = await ApplicationPolicyReferenceSchema.References.QueryAsync(Transaction,
            ApplicationPolicyReferenceSchema.ByPolicyVersion.Query()
                .WithPrefix(policyId.ToString(), versionKey).Take(MaximumApplicabilityEntries + 1),
            ct).ConfigureAwait(false);
        if (page.NextCursor is not null)
            throw new InvalidOperationException(
                "A policy version has more applicability rows than the projection bound.");
        foreach (var reference in page.Items)
            await ApplicationPolicyReferenceSchema.References.ReplaceAsync(Transaction,
                reference, update(reference), ct).ConfigureAwait(false);
    }

    async ValueTask DeleteReferencesAsync(Uuid policyId, string versionKey,
        CancellationToken ct)
    {
        var page = await ApplicationPolicyReferenceSchema.References.QueryAsync(Transaction,
            ApplicationPolicyReferenceSchema.ByPolicyVersion.Query()
                .WithPrefix(policyId.ToString(), versionKey).Take(MaximumApplicabilityEntries + 1),
            ct).ConfigureAwait(false);
        if (page.NextCursor is not null)
            throw new InvalidOperationException(
                "A policy version has more applicability rows than the projection bound.");
        foreach (var reference in page.Items)
            await ApplicationPolicyReferenceSchema.References.DeleteAsync(Transaction,
                (reference.PolicyId, versionKey, reference.EntryId), ct).ConfigureAwait(false);
    }

    async ValueTask InsertReferencesAsync(ApplicationPolicyReferenceState state, long? version,
        string versionKey, string status, long revision, DateOnly? effectiveFrom,
        DateOnly? effectiveUntil, bool hasPendingSuccessor, CancellationToken ct)
    {
        var content = version is null ? state.DraftContent : state.CurrentContent;
        if (content is null)
            throw new InvalidOperationException(
                "A policy applicability projection requires its source content metadata.");
        foreach (var applicability in content.Applicability)
        {
            if (!TryResolve(applicability, out var subjectType, out var recordId))
                continue;
            var entryId = Uuid.CreateVersion5(state.PolicyId,
                $"application-policy-reference:{versionKey}:{Policy.Key(applicability)}");
            await ApplicationPolicyReferenceSchema.References.InsertAsync(Transaction,
                new ApplicationPolicyReferenceView(state.TenantId, subjectType, recordId,
                    state.ProgramId, state.PolicyId, state.Identifier, content.Title, version,
                    revision, status, effectiveFrom, effectiveUntil, entryId,
                    applicability.Subject, hasPendingSuccessor,
                    version is null ? null : state.PendingRetirementEffectiveUntil), ct)
                .ConfigureAwait(false);
        }
    }

    async ValueTask<ApplicationPolicyReferenceState> RequireStateAsync(Uuid tenantId,
        Uuid programId, Uuid policyId, CancellationToken ct)
    {
        var state = await ApplicationPolicyReferenceSchema.States.GetAsync(Transaction, policyId,
            ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A policy event cannot project before its draft was created.");
        if (state.TenantId != tenantId || state.ProgramId != programId)
            throw new InvalidOperationException(
                "A policy event does not match the tenant-scoped policy reference state.");
        return state;
    }

    async ValueTask ReplaceStateAsync(ApplicationPolicyReferenceState current,
        ApplicationPolicyReferenceState replacement, CancellationToken ct)
    {
        await ApplicationPolicyReferenceSchema.States.ReplaceAsync(Transaction, current,
            replacement, ct).ConfigureAwait(false);
        await UpdateCoverageAsync(current.TenantId, current.UnresolvedReferenceCount,
            replacement.UnresolvedReferenceCount, ct).ConfigureAwait(false);
    }

    async ValueTask UpdateCoverageAsync(Uuid tenantId, int previousCount, int nextCount,
        CancellationToken ct)
    {
        var current = await ApplicationPolicyReferenceSchema.Coverage.GetAsync(Transaction,
            tenantId, ct).ConfigureAwait(false);
        var count = (current?.UnresolvedReferenceCount ?? 0) - previousCount + nextCount;
        if (count < 0)
            throw new InvalidOperationException(
                "The tenant policy-reference coverage count cannot become negative.");
        if (current is null)
        {
            if (count > 0)
                await ApplicationPolicyReferenceSchema.Coverage.InsertAsync(Transaction,
                    new ApplicationPolicyReferenceCoverage(tenantId, count), ct)
                    .ConfigureAwait(false);
            return;
        }
        if (count == 0)
            await ApplicationPolicyReferenceSchema.Coverage.DeleteAsync(Transaction, tenantId, ct)
                .ConfigureAwait(false);
        else
            await ApplicationPolicyReferenceSchema.Coverage.ReplaceAsync(Transaction, current,
                current with { UnresolvedReferenceCount = count }, ct).ConfigureAwait(false);
    }

    void EnsureTenant(Uuid tenantId)
    {
        if (_batchTenantId != tenantId)
            throw new InvalidOperationException(
                "A policy event must belong to its tenant projection batch.");
    }

    static ApplicationPolicyReferenceContent ReferenceContent(PolicyContent content) =>
        new(content.Title, (content.Applicability ?? []).ToArray());

    static int CountUnresolved(ApplicationPolicyReferenceContent? content) => content is null
        ? 0
        : content.Applicability.Count(static reference => !TryResolve(reference, out _, out _));

    static int CountUnresolved(ApplicationPolicyReferenceContent? current,
        ApplicationPolicyReferenceContent? draft, int historical) =>
        historical + CountUnresolved(current) + CountUnresolved(draft);

    static bool TryResolve(PolicyApplicabilityReference reference, out string subjectType,
        out Uuid recordId)
    {
        subjectType = reference.SubjectType;
        recordId = reference.RecordId ?? Uuid.Empty;
        return (reference.SubjectType is "application" or "system_instance") &&
               reference.RecordId is { } id && id != Uuid.Empty;
    }

    public async ValueTask<Page<ApplicationPolicyReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationPolicyReferenceSchema.References.QueryAsync(tx,
            ApplicationPolicyReferenceSchema.ByRecord.Query()
                .WithPrefix(subjectType, recordId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> HasUnresolvedReferencesAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var coverage = await ApplicationPolicyReferenceSchema.Coverage.GetAsync(tx, tenantId, ct)
            .ConfigureAwait(false);
        return coverage is { UnresolvedReferenceCount: > 0 };
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policies")), ct);
}
