using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reports the known boundary impact without authorizing an application change.</summary>
public sealed class PreviewApplicationChangeHandler(
    IAggregateReader aggregates, IApplicationDirectoryReader applications,
    IApplicationBoundaryReferenceDirectory references,
    ApplicationBoundaryReferenceReadConsistency boundaryConsistency,
    IApplicationControlDraftReferenceDirectory controls,
    ApplicationControlDraftReferenceReadConsistency controlConsistency,
    RestrictedApplicationVisibility visibility,
    ApplicationGovernedRevisionGate? gate = null,
    SystemInstanceReadConsistency? instanceConsistency = null,
    IApplicationIncomingRelationshipDirectory? incomingRelationshipDirectory = null,
    ApplicationIncomingRelationshipReadConsistency? incomingRelationshipConsistency = null)
    : IRequestHandler<PreviewApplicationChange, ApplicationChangePreview>,
      IApplicationChangeImpactReader, IApplicationImportRetirementImpactReader
{
    const int MaximumSystemInstanceControlDraftReferences = 200;

    public PreviewApplicationChangeHandler(IAggregateReader aggregates,
        IApplicationDirectoryReader applications,
        IApplicationBoundaryReferenceDirectory references,
        ApplicationBoundaryReferenceReadConsistency boundaryConsistency,
        IApplicationControlDraftReferenceDirectory controls,
        ApplicationControlDraftReferenceReadConsistency controlConsistency,
        RestrictedApplicationVisibility visibility,
        SystemInstanceReadConsistency instanceConsistency)
        : this(aggregates, applications, references, boundaryConsistency, controls,
            controlConsistency, visibility, null, instanceConsistency)
    {
    }

    static readonly string[] MissingContextsBeforeSystemInstanceControls =
        ["vendors", "approved_control_versions_and_lifecycle_impact"];

    static readonly string[] MissingContextsAfterSystemInstanceControls =
        ["policies", "evidence_sources", "access_populations", "review_campaigns",
            "open_work", "readiness", "engagements", "cross_source_manual_reliance"];

    public async ValueTask<Result<ApplicationChangePreview>> HandleAsync(
        IRequestContext<PreviewApplicationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        return await ReadAsync(request, userId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ApplicationChangePreview>> ReadAsync(
        PreviewApplicationChange request, Uuid userId, CancellationToken ct)
        => await ReadCoreAsync(request, userId, requireActorVisibility: true, ct).ConfigureAwait(false);

    public ValueTask<Result<ApplicationChangePreview>> ReadForImportWorkerAsync(
        PreviewApplicationChange request, CancellationToken ct) =>
        ReadCoreAsync(request, Uuid.Empty, requireActorVisibility: false, ct);

    async ValueTask<Result<ApplicationChangePreview>> ReadCoreAsync(
        PreviewApplicationChange request, Uuid userId, bool requireActorVisibility,
        CancellationToken ct)
    {
        if ((incomingRelationshipDirectory is null) !=
            (incomingRelationshipConsistency is null))
            throw new InvalidOperationException(
                "The incoming application relationship reader and consistency fence must be registered together.");

        var inputError = Validate(request);
        if (inputError is not null)
            return Result<ApplicationChangePreview>.Failure(inputError);

        var source = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (requireActorVisibility && !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (source.IsRetired)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application is retired."));
        if (source.Revision != request.ExpectedApplicationRevision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                $"The application is at revision {source.Revision}; preview revision {request.ExpectedApplicationRevision} is stale."));

        ApplicationRevisionContent? content = null;
        if (request.ChangeKind == "revise")
        {
            var invalid = source.ResolveRevisionContent(request.ExpectedApplicationRevision,
                request.Name!, request.Purpose!, request.OwnerReference, request.Classification,
                request.SystemOwnerPersonId, request.AccessOwnerPersonId, null,
                request.ApplicationKind, request.Criticality, gate?.IsEnabled ?? false,
                request.ClassificationIntent, request.SystemOwnerIntent, request.AccessOwnerIntent,
                out var resolved);
            if (invalid is not null)
                return Result<ApplicationChangePreview>.Failure(invalid.ToRequestError());
            var owners = await ApplicationOwnerReferences.ValidateAsync(aggregates,
                request.TenantId, resolved.SystemOwnerPersonId, resolved.AccessOwnerPersonId, ct)
                .ConfigureAwait(false);
            if (owners is not null)
                return Result<ApplicationChangePreview>.Failure(owners);
            content = resolved;
        }

        var current = await applications.GetAsync(request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        if (current is null || current.Revision < source.Revision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application projection has not reached the source revision.",
                isTransient: true));
        if (current.TenantId != request.TenantId || current.ApplicationId != request.ApplicationId)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (current.Revision != source.Revision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application source and projection changed during preview.",
                isTransient: true));

        var relationshipSourceRevision = source.SourceRelationshipRevision;
        var relationships = await ApplicationRelationshipProjection.ReadPreviewAsync(source,
            request.TenantId, userId, visibility, ct).ConfigureAwait(false);
        IReadOnlyList<ApplicationIncomingRelationshipView> incomingRelationships = [];
        ProjectionCheckpoint? incomingRelationshipFence = null;
        var incomingRelationshipsOverLimit = false;
        if (incomingRelationshipDirectory is not null &&
            incomingRelationshipConsistency is not null)
        {
            var captured = await incomingRelationshipConsistency.CaptureAsync(request.TenantId,
                ct).ConfigureAwait(false);
            if (!captured.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(captured.Error);
            incomingRelationshipFence = captured.Value;

            var candidates = await incomingRelationshipDirectory.ListActiveIncomingAsync(
                request.TenantId, request.ApplicationId, 200, null, ct).ConfigureAwait(false);
            if (candidates.Items.Count > 200)
                return Result<ApplicationChangePreview>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The application incoming-relationship projection is inconsistent.",
                    isTransient: true));
            incomingRelationshipsOverLimit = candidates.NextCursor is not null;
            var visible = new List<ApplicationIncomingRelationshipView>(candidates.Items.Count);
            foreach (var candidate in candidates.Items)
            {
                if (!IsValidIncomingRelationship(candidate, request.TenantId,
                        request.ApplicationId))
                    return Result<ApplicationChangePreview>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The application incoming-relationship projection is inconsistent.",
                        isTransient: true));
                if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                        candidate.SourceApplicationId, ct).ConfigureAwait(false))
                    continue;
                visible.Add(new ApplicationIncomingRelationshipView(
                    candidate.SourceApplicationId, candidate.RelationshipId,
                    candidate.RelationshipType, candidate.ActorMemberId,
                    candidate.ActorDisplay, candidate.ChangedAt));
            }
            incomingRelationships = visible
                .OrderBy(static item => item.SourceApplicationId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.RelationshipId.ToString(), StringComparer.Ordinal)
                .ToArray();
        }

        var boundaryFence = await boundaryConsistency.CaptureAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!boundaryFence.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(boundaryFence.Error);
        var controlFence = await controlConsistency.CaptureAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!controlFence.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(controlFence.Error);
        ProjectionCheckpoint? instanceFence = null;
        if (instanceConsistency is not null)
        {
            var capturedInstances = await instanceConsistency.CaptureApplicationListAsync(
                request.TenantId, ct).ConfigureAwait(false);
            if (!capturedInstances.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(capturedInstances.Error);
            instanceFence = capturedInstances.Value;
        }
        var page = await references.ListAsync(request.TenantId, "application",
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.SubjectType != "application" ||
                                   item.GovernedRecordId != request.ApplicationId))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application boundary reference projection is inconsistent.",
                isTransient: true));
        var controlPage = await controls.ListAsync(request.TenantId, "application",
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (controlPage.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.SubjectType != "application" ||
                                          item.GovernedRecordId != request.ApplicationId))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The application control draft reference projection is inconsistent.",
                isTransient: true));

        var instancePage = await applications.ListInstancesAsync(request.TenantId,
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (instancePage.Items.Count > 200 ||
            instancePage.Items.Any(item => item.TenantId != request.TenantId ||
                                           item.ApplicationId != request.ApplicationId ||
                                           item.SystemInstanceId == Uuid.Empty) ||
            instancePage.Items.Select(static item => item.SystemInstanceId).Distinct().Count() !=
            instancePage.Items.Count)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The system instance projection is inconsistent.",
                isTransient: true));

        var systemInstanceControlDraftReferences =
            new List<ApplicationControlDraftReferenceView>();
        var systemInstanceControlDraftReferencesOverLimit = instancePage.NextCursor is not null;
        if (instanceConsistency is not null)
        {
            foreach (var instance in instancePage.Items)
            {
                var remaining = MaximumSystemInstanceControlDraftReferences -
                    systemInstanceControlDraftReferences.Count;
                if (remaining == 0)
                {
                    systemInstanceControlDraftReferencesOverLimit = true;
                    break;
                }

                var instanceControls = await controls.ListAsync(request.TenantId,
                    "system_instance", instance.SystemInstanceId,
                    Math.Min(200, remaining), null, ct).ConfigureAwait(false);
                if (instanceControls.Items.Count > remaining ||
                    instanceControls.Items.Any(reference =>
                        !IsValidSystemInstanceControlReference(reference, request.TenantId,
                            instance.SystemInstanceId)))
                    return Result<ApplicationChangePreview>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The system-instance control reference projection is inconsistent.",
                        isTransient: true));

                systemInstanceControlDraftReferences.AddRange(instanceControls.Items);
                if (instanceControls.NextCursor is not null)
                {
                    systemInstanceControlDraftReferencesOverLimit = true;
                    break;
                }
            }
        }

        var boundaryConfirmed = await boundaryConsistency.ConfirmUnchangedAndCaughtUpAsync(
            request.TenantId, boundaryFence.Value, ct).ConfigureAwait(false);
        if (!boundaryConfirmed.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(boundaryConfirmed.Error);
        var controlsConfirmed = await controlConsistency.ConfirmUnchangedAndCaughtUpAsync(
            request.TenantId, controlFence.Value, ct).ConfigureAwait(false);
        if (!controlsConfirmed.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(controlsConfirmed.Error);
        if (instanceFence is { } capturedInstanceFence)
        {
            var instancesConfirmed = await instanceConsistency!
                .ConfirmApplicationListUnchangedAndCaughtUpAsync(request.TenantId,
                    capturedInstanceFence, ct).ConfigureAwait(false);
            if (!instancesConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(instancesConfirmed.Error);
        }
        if (incomingRelationshipFence is { } capturedRelationshipFence)
        {
            var relationshipsConfirmed = await incomingRelationshipConsistency!
                .ConfirmUnchangedAndCaughtUpAsync(request.TenantId, capturedRelationshipFence, ct)
                .ConfigureAwait(false);
            if (!relationshipsConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(relationshipsConfirmed.Error);
        }

        // Source rechecks catch a pending application or relationship write that raced the reads.
        var latest = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (latest.Revision != source.Revision ||
            latest.SourceRelationshipRevision != relationshipSourceRevision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application or its relationships changed during preview.",
                isTransient: true));

        List<string> pending = [];
        if (page.NextCursor is not null)
            pending.Add("boundary_references_over_limit");
        if (controlPage.NextCursor is not null)
            pending.Add("control_draft_references_over_limit");
        if (instancePage.NextCursor is not null)
            pending.Add("system_instance_references_over_limit");
        if (relationships.HasMore || incomingRelationshipsOverLimit)
            pending.Add("application_relationships_over_limit");
        pending.AddRange(MissingContextsBeforeSystemInstanceControls);
        if (incomingRelationshipDirectory is null)
            pending.Add("application_relationships");
        if (instanceConsistency is null)
            pending.Add("system_instance_control_draft_references");
        else if (systemInstanceControlDraftReferencesOverLimit)
            pending.Add("system_instance_control_draft_references_over_limit");
        pending.AddRange(MissingContextsAfterSystemInstanceControls);
        var preview = new ApplicationChangePreview(
            request.TenantId, request.ApplicationId, source.Revision, request.ChangeKind,
            Changes(current, content), page.Items, pending, false)
        {
            ApplicationRelationships = relationships.Items,
            IncomingApplicationRelationships = incomingRelationships,
            ControlDraftReferences = controlPage.Items.Concat(systemInstanceControlDraftReferences)
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            SystemInstanceReferences = instancePage.Items,
        };
        return Result<ApplicationChangePreview>.Success(preview with
        {
            ImpactDigest = ApplicationChangeImpactDigest.Compute(preview),
        });
    }

    static RequestError? Validate(PreviewApplicationChange request)
    {
        if (request.ExpectedApplicationRevision < 1)
            return new RequestError(RequestErrorKind.Validation,
                "The expected application revision must be positive.");
        if (request.ChangeKind == "retire")
            return request.Name is null && request.Purpose is null &&
                   request.OwnerReference is null && request.Classification is null &&
                   request.SystemOwnerPersonId is null && request.AccessOwnerPersonId is null &&
                   request.ApplicationKind is null && request.Criticality is null ? null :
                new RequestError(RequestErrorKind.Validation,
                    "A retirement preview cannot include revised application fields.");
        if (request.ChangeKind != "revise")
            return new RequestError(RequestErrorKind.Validation,
                "The change kind must be revise or retire.");
        return null;
    }

    static bool IsValidIncomingRelationship(ApplicationRelationshipView relationship,
        Uuid tenantId, Uuid targetApplicationId)
    {
        if (relationship.TenantId != tenantId ||
            relationship.RelationshipId == Uuid.Empty ||
            relationship.SourceApplicationId == Uuid.Empty ||
            relationship.SourceApplicationId == targetApplicationId ||
            relationship.TargetApplicationId != targetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(relationship.RelationshipType) ||
            relationship.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(relationship.ActorDisplay) ||
            relationship.ChangedAt == default)
            return false;

        return relationship.RelationshipType switch
        {
            "depends_on" => relationship.Status == "active",
            "replaces" => relationship.Status is "active" or "proposed" or "approved",
            _ => false,
        };
    }

    static bool IsValidSystemInstanceControlReference(
        ApplicationControlDraftReferenceView reference, Uuid tenantId, Uuid systemInstanceId) =>
        reference.TenantId == tenantId && reference.SubjectType == "system_instance" &&
        reference.GovernedRecordId == systemInstanceId && reference.ProgramId != Uuid.Empty &&
        reference.ControlId != Uuid.Empty && reference.EntryId != Uuid.Empty &&
        reference.Revision > 0 && !string.IsNullOrWhiteSpace(reference.Identifier) &&
        !string.IsNullOrWhiteSpace(reference.Subject) &&
        !string.IsNullOrWhiteSpace(reference.Rationale);

    static List<ApplicationFieldChange> Changes(ApplicationView current,
        ApplicationRevisionContent? content)
    {
        if (content is null)
            return [];
        var changes = new List<ApplicationFieldChange>();
        Add("name", current.Name, content.Name);
        Add("purpose", current.Purpose, content.Purpose);
        Add("owner_reference", current.OwnerReference, content.OwnerReference);
        Add("classification", current.Classification, content.Classification);
        Add("system_owner_person_id", current.SystemOwnerPersonId?.ToString(),
            content.SystemOwnerPersonId?.ToString());
        Add("access_owner_person_id", current.AccessOwnerPersonId?.ToString(),
            content.AccessOwnerPersonId?.ToString());
        Add("application_kind", current.ApplicationKind, content.ApplicationKind);
        Add("criticality", current.Criticality, content.Criticality);
        return changes;

        void Add(string field, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                changes.Add(new ApplicationFieldChange(field, before, after));
        }
    }
}
