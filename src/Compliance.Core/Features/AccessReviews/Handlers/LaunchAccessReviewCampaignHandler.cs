using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Builds one item per effective access row of each assigned accepted population, with the classification and
///     variance known now, and freezes items, reviewers, instructions, and deadline as an immutable snapshot and as
///     complete verified manifests. The launch is the root native fact of the campaign: its first response is the
///     original registration, so an exact retry returns it even after the campaign changed. Materializing the items
///     is the launch fan-out's work. Later classification or expectation changes never alter a launched campaign.
/// </summary>
public sealed class LaunchAccessReviewCampaignHandler(NativeOperationExecutor operations, IAggregateReader reader, IDomainEventReader events,
    AccessReviewReader campaigns, PopulationSnapshotFreezer freezer, IAccessReviewSources sources,
    IImmutableContentStore content, AccessExpectationsReader expectations, TimeProvider clock, RestrictedApplicationVisibility visibility,
    AccessReviewQueueEligibility queueEligibility, OperatingAuthority authority)
    : IRequestHandler<LaunchAccessReviewCampaign, AccessReviewCampaignRegistration>
{
    const string CommandKind = nameof(LaunchAccessReviewCampaign);

    public async ValueTask<Result<AccessReviewCampaignRegistration>> HandleAsync(
        IRequestContext<LaunchAccessReviewCampaign> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessReviewCampaignRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact campaign source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessReviewCampaignRegistration>> HandleCoreAsync(
        IRequestContext<LaunchAccessReviewCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaignId = request.CampaignId;
        if (campaignId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A campaign launch requires a campaign id.");
        var actor = AccessReviewActor.From(context);
        var now = clock.GetUtcNow();
        var stream = AccessReviewEffects.CampaignStream(request.TenantId, campaignId);
        var provenance = AccessReviewEffects.Root(campaignId, CommandKind, stream, actor.Reference, null,
            AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var hydrated = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId, campaignId, campaignId), ct)
            .ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        Frozen? frozen = null;
        AccessReviewPreparedCut? preparedCut = null;
        if (hydrated.Launched is { } existing)
        {
            // An already accepted launch resolves from the owner's facts: the actor must currently see and manage it.
            var launch = await campaigns.LaunchOfAsync(request.TenantId, existing, ct).ConfigureAwait(false);
            foreach (var systemInstanceId in launch.Items.Select(static item => item.SystemInstanceId).Distinct())
                if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, systemInstanceId, ct)
                        .ConfigureAwait(false))
                    return Failure(RequestErrorKind.NotFound, "The campaign was not found.");
            if (launch.ProgramId is not { } ownerProgramId || ownerProgramId == Uuid.Empty)
                return Failure(RequestErrorKind.Conflict, "The campaign already exists without a routed owner Program.");
            var ownerProgram = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, ownerProgramId), ct)
                .ConfigureAwait(false);
            if (!ownerProgram.IsCreated)
                return Failure(RequestErrorKind.NotFound, "The owner Program was not found in this tenant.");
            if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, ownerProgramId, actor.MemberId, ct)
                    .ConfigureAwait(false))
                return Failure(RequestErrorKind.Forbidden, "The launching actor must currently manage the owner Program.");
        }
        else
        {
            if (request.ProgramId is not { } requestedProgramId || requestedProgramId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "A routed campaign requires an explicit owner Program and remediation owner.");
            var requestedProgram = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, requestedProgramId), ct)
                .ConfigureAwait(false);
            if (!requestedProgram.IsCreated)
                return Failure(RequestErrorKind.NotFound, "The owner Program was not found in this tenant.");
            if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, requestedProgramId,
                    actor.MemberId, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Forbidden, "The launching actor must currently manage the owner Program.");
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessReviewCampaign>(binding,
            () => new AccessReviewCampaign(request.TenantId, campaignId, campaignId), owner => owner.Acceptance,
            async (owner, prepareCt) =>
            {
                var prepared = owner.PrepareLaunch();
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var built = await FreezeAsync(context, request, actor, provenance, now, prepareCt).ConfigureAwait(false);
                if (!built.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(built.Error!);
                frozen = built.Value!;
                response = frozen.Response;
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response);
            },
            (AccessReviewCampaign owner, ImmutableContentRef firstResponse) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return AccessReviewPreparedCut.Changed();
                return owner.Launch(provenance,
                request.Name, request.Instructions, request.Deadline, frozen!.SnapshotId, frozen.ContentSha256,
                frozen.Reviewers, frozen.Items, frozen.ItemIntents, actor.Reference, now, request.ProgramId,
                request.RemediationOwnerMemberId, firstResponse, frozen.Result);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessReviewCampaignRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<Frozen>> FreezeAsync(IRequestContext<LaunchAccessReviewCampaign> context,
        LaunchAccessReviewCampaign request, AccessReviewActor actor, OperationProvenance provenance, DateTimeOffset now,
        CancellationToken ct)
    {
        var campaignId = request.CampaignId;
        if (request.ProgramId is not { } programId || programId == Uuid.Empty ||
            request.RemediationOwnerMemberId is not { } remediationOwnerMemberId ||
            remediationOwnerMemberId == Uuid.Empty)
            return Fail(RequestErrorKind.Validation,
                "A routed campaign requires an explicit owner Program and remediation owner.");
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, programId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            return Fail(RequestErrorKind.NotFound, "The owner Program was not found in this tenant.");
        if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, programId, actor.MemberId, ct)
                .ConfigureAwait(false))
            return Fail(RequestErrorKind.Forbidden, "The launching actor must currently manage the owner Program.");
        if (request.Assignments is not { Count: > 0 } ||
            request.Assignments.Select(static assignment => assignment.PopulationId).Distinct().Count() !=
            request.Assignments.Count)
            return Fail(RequestErrorKind.Validation, "A campaign assigns one or more distinct accepted populations.");
        if (!AccessReviewVocabulary.IsBoundedText(request.Name, 200) ||
            !AccessReviewVocabulary.IsBoundedText(request.Instructions, 8000) || request.Deadline <= now)
            return Fail(RequestErrorKind.Validation, "A campaign requires a name, instructions, and a future deadline.");

        var reviewers = new List<AccessReviewerView>();
        var items = new List<AccessReviewItemView>();
        var correlated = new Dictionary<Uuid, Uuid?>();
        foreach (var assignment in request.Assignments)
        {
            if (assignment.ReviewerMemberId == Uuid.Empty)
                return Fail(RequestErrorKind.Validation, "Each assignment names a reviewer.");
            var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId, assignment.PopulationId),
                ct).ConfigureAwait(false);
            if (population.Opened is not { } opened ||
                !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId, opened.ApplicationId,
                    opened.SystemInstanceId, ct).ConfigureAwait(false))
                return Fail(RequestErrorKind.NotFound, "The population was not found.");
            var loaded = await AcceptedAccessPopulation.LoadAsync(reader, events, content, request.TenantId, assignment.PopulationId,
                ct).ConfigureAwait(false);
            if (!loaded.IsSuccess)
                return Result<Frozen>.Failure(loaded.Error);
            var accepted = loaded.Value;
            if (!await queueEligibility.CanRemediateAsync(request.TenantId, programId, remediationOwnerMemberId,
                    accepted.Header.SystemInstanceId, ct).ConfigureAwait(false))
                return Fail(RequestErrorKind.Validation,
                    "The remediation owner must be active, have program-queue read and access-review management authority, and see every restricted system.");
            if (assignment.ReviewerMemberId == remediationOwnerMemberId)
                return Fail(RequestErrorKind.Validation,
                    "The remediation owner must be distinct from every frozen reviewer.");
            if (!await queueEligibility.CanReviewAsync(request.TenantId, programId, assignment.ReviewerMemberId,
                    accepted.Header.SystemInstanceId, ct).ConfigureAwait(false))
                return Fail(RequestErrorKind.Validation,
                    "Each reviewer must be an active tenant member who can read the program queue and every assigned restricted system.");
            var owner = await sources.AccessOwnerMemberAsync(request.TenantId, accepted.Header.ApplicationId, ct)
                .ConfigureAwait(false);
            if (!owner.IsSuccess)
                return Result<Frozen>.Failure(owner.Error);
            var delegated = owner.Value != assignment.ReviewerMemberId;
            if (delegated && !AccessReviewVocabulary.IsBoundedText(assignment.DelegationReason, 2000))
                return Fail(RequestErrorKind.Validation,
                    "A reviewer other than the application's access owner is a delegate and requires a delegation reason.");
            reviewers.Add(new AccessReviewerView(accepted.Population.Id, accepted.Header.SystemInstanceId,
                assignment.ReviewerMemberId, delegated, delegated ? assignment.DelegationReason!.Trim() : null));

            var variance = await GetAccessVarianceHandler.EvaluateAsync(expectations, request.TenantId, accepted, ct)
                .ConfigureAwait(false);
            var principals = accepted.Facts.Principals.ToDictionary(static principal => principal.ProviderSubjectId,
                StringComparer.Ordinal);
            var entitlements = accepted.Facts.Entitlements.ToDictionary(
                static entitlement => entitlement.ProviderEntitlementId, StringComparer.Ordinal);
            foreach (var row in accepted.EffectiveAccess)
            {
                var principal = principals[row.ProviderSubjectId];
                var entitlement = entitlements[row.ProviderEntitlementId];
                var classification = accepted.CurrentClassification(row.ProviderSubjectId);
                var personId = classification?.Classification == AccessReviewVocabulary.Human
                    ? classification.PersonId
                    : null;
                Uuid? userId = null;
                if (personId is { } id)
                {
                    if (!correlated.TryGetValue(id, out userId))
                        correlated[id] = userId = (await reader.HydrateAsync(new Person(request.TenantId, id), ct)
                            .ConfigureAwait(false)).CorrelatedUserId;
                }

                var explained = variance.FirstOrDefault(item => item.ProviderSubjectId == row.ProviderSubjectId &&
                                                                item.ProviderEntitlementId == row.ProviderEntitlementId);
                items.Add(new AccessReviewItemView(ItemId(campaignId, accepted.Population.Id, row),
                    accepted.Population.Id, accepted.Snapshot.Id, accepted.Header.SystemInstanceId,
                    assignment.ReviewerMemberId, row.ProviderSubjectId, principal.PrincipalKind,
                    principal.DisplayName, classification?.Classification ?? AccessReviewVocabulary.Unclassified,
                    personId, userId, row.ProviderEntitlementId, entitlement.EntitlementKind, entitlement.DisplayName,
                    explained?.Privileged == true, explained?.Category ?? AccessVarianceEvaluator.Unexpected,
                    row.Paths, row.ExpiresAt));
            }
        }

        if (items.Count == 0)
            return Fail(RequestErrorKind.Validation, "The assigned populations have no effective access to review.");

        var header = new AccessReviewCampaignHeader(campaignId, request.Name.Trim(), request.Instructions.Trim(),
            request.Deadline, programId, remediationOwnerMemberId);
        var snapshot = await freezer.FreezeAsync(context, request.TenantId, AccessReviewCampaignContent.LaunchKind,
            AccessReviewCampaignContent.LaunchRows(header, reviewers, items), null, null, actor.Reference, ct)
            .ConfigureAwait(false);
        if (!snapshot.IsSuccess)
            return Result<Frozen>.Failure(snapshot.Error);
        var frozenSnapshot = await reader.HydrateAsync(new PopulationSnapshot(request.TenantId, snapshot.Value.SnapshotId),
            ct).ConfigureAwait(false);
        if (frozenSnapshot.FrozenFact is not { } frozenFact)
            return Fail(RequestErrorKind.Conflict, "The launch snapshot is not visible yet.", true);
        var proof = NativeFactReference.Of(frozenSnapshot.Stream, frozenFact);

        var tenantId = request.TenantId;
        var reviewersManifest = await AccessReviewContent.WriteAsync<IReadOnlyList<AccessReviewerView>>(content,
            tenantId, AccessReviewContent.ReviewersSchema, reviewers, ct).ConfigureAwait(false);
        var itemsManifest = await AccessReviewContent.WriteAsync<IReadOnlyList<AccessReviewItemView>>(content,
            tenantId, AccessReviewContent.ItemsSchema, items, ct).ConfigureAwait(false);
        var intentHeader = new DurableIntentHeader(campaignId, provenance.Owner, provenance.Owner, provenance.Owner,
            provenance.Actor, provenance.Episode, null, provenance.AuthoredIntentSha256, now);
        var selections = new SortedDictionary<string, AccessReviewItemSelection>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var entry = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewContent.ItemEntrySchema,
                item, ct).ConfigureAwait(false);
            var intent = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewItemCreationIntent.Schema,
                new AccessReviewItemCreationIntent(intentHeader, campaignId, (ulong)AccessReviewEffects.Generation,
                    item.ItemId, item.SystemInstanceId, entry, item.ReviewerMemberId,
                    AccessReviewEffects.ReviewerEpisode(campaignId, item.ItemId), proof), ct).ConfigureAwait(false);
            if (!selections.TryAdd(item.ItemId.ToString(), new AccessReviewItemSelection(item.ItemId, intent)))
                return Fail(RequestErrorKind.Conflict, "Two effective access rows freeze one item.");
        }

        var itemIntents = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewItemIntentManifest.Schema,
            new AccessReviewItemIntentManifest([.. selections.Values]), ct).ConfigureAwait(false);
        var registration = new AccessReviewCampaignRegistration(campaignId, snapshot.Value.SnapshotId,
            snapshot.Value.ContentSha256, items.Count);
        var (response, result) = await AccessReviewContent.FreezeOkAsync(content, tenantId,
            AccessReviewContent.CampaignRegistrationSchema, registration, ct).ConfigureAwait(false);
        return Result<Frozen>.Success(new Frozen(response, result, snapshot.Value.SnapshotId,
            snapshot.Value.ContentSha256, reviewersManifest, itemsManifest, itemIntents));
    }

    static Uuid ItemId(Uuid campaignId, Uuid populationId, EffectiveAccessView row) =>
        Uuid.CreateVersion5(campaignId, $"{populationId}\n{row.ProviderSubjectId}\n{row.ProviderEntitlementId}");

    static Result<AccessReviewCampaignRegistration> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessReviewCampaignRegistration>(kind, message);

    static Result<Frozen> Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result<Frozen>.Failure(new RequestError(kind, message, transient));

    sealed record Frozen(FirstResponseDescriptor Response, ImmutableContentRef Result, Uuid SnapshotId,
        string ContentSha256, ImmutableContentRef Reviewers, ImmutableContentRef Items, ImmutableContentRef ItemIntents);
}
