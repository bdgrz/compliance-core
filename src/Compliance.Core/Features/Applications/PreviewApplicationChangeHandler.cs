using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.AccessReviews;
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
    ApplicationIncomingRelationshipReadConsistency? incomingRelationshipConsistency = null,
    IApplicationControlVersionReferenceDirectory? controlVersions = null,
    ApplicationControlVersionReferenceReadConsistency? controlVersionConsistency = null,
    IApplicationPolicyReferenceDirectory? policyReferences = null,
    ApplicationPolicyReferenceReadConsistency? policyReferenceConsistency = null,
    IApplicationEvidenceSourceReferenceDirectory? evidenceSources = null,
    ApplicationEvidenceSourceReferenceReadConsistency? evidenceSourceConsistency = null,
    IApplicationEvidenceSourceAccess? evidenceSourceAccess = null,
    IAccessPopulationDirectoryReader? accessPopulations = null,
    ApplicationAccessPopulationReadConsistency? accessPopulationConsistency = null,
    IAccessReviewCampaignImpactDirectoryReader? reviewCampaigns = null,
    ApplicationReviewCampaignReadConsistency? reviewCampaignConsistency = null)
    : IRequestHandler<PreviewApplicationChange, ApplicationChangePreview>,
      IApplicationChangeImpactReader, IApplicationImportRetirementImpactReader
{
    const int MaximumSystemInstanceBoundaryReferences = 200;
    const int MaximumSystemInstanceControlDraftReferences = 200;
    const int MaximumApplicationControlVersionReferences = 200;
    const int MaximumSystemInstanceControlVersionReferences = 200;
    const int MaximumApplicationPolicyReferences = 200;
    const int MaximumSystemInstancePolicyReferences = 200;
    const int MaximumApplicationEvidenceSourceReferences = 200;
    const int MaximumApplicationAccessPopulationReferences = 200;
    const int MaximumApplicationReviewCampaignReferences = 200;

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
        ["vendors"];

    static readonly string[] MissingContextsAfterSystemInstanceControls =
        ["open_work",
            "readiness", "engagements", "cross_source_manual_reliance"];

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
        if ((controlVersions is null) != (controlVersionConsistency is null))
            throw new InvalidOperationException(
                "The approved control-version reader and consistency fence must be registered together.");
        if ((policyReferences is null) != (policyReferenceConsistency is null))
            throw new InvalidOperationException(
                "The policy-reference reader and consistency fence must be registered together.");
        if ((evidenceSources is null) != (evidenceSourceConsistency is null))
            throw new InvalidOperationException(
                "The evidence-source reader and consistency fence must be registered together.");
        if ((accessPopulations is null) != (accessPopulationConsistency is null))
            throw new InvalidOperationException(
                "The access-population reader and consistency fence must be registered together.");
        if ((reviewCampaigns is null) != (reviewCampaignConsistency is null))
            throw new InvalidOperationException(
                "The review-campaign reader and consistency fence must be registered together.");

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
        ProjectionCheckpoint? controlVersionFence = null;
        if (controlVersionConsistency is not null)
        {
            var capturedControlVersions = await controlVersionConsistency.CaptureAsync(
                request.TenantId, ct).ConfigureAwait(false);
            if (!capturedControlVersions.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(capturedControlVersions.Error);
            controlVersionFence = capturedControlVersions.Value;
        }
        ProjectionCheckpoint? policyReferenceFence = null;
        if (policyReferenceConsistency is not null)
        {
            var capturedPolicyReferences = await policyReferenceConsistency.CaptureAsync(
                request.TenantId, ct).ConfigureAwait(false);
            if (!capturedPolicyReferences.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(capturedPolicyReferences.Error);
            policyReferenceFence = capturedPolicyReferences.Value;
        }
        ProjectionCheckpoint? evidenceSourceFence = null;
        if (evidenceSourceConsistency is not null)
        {
            var capturedEvidenceSources = await evidenceSourceConsistency.CaptureAsync(
                request.TenantId, ct).ConfigureAwait(false);
            if (!capturedEvidenceSources.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(capturedEvidenceSources.Error);
            evidenceSourceFence = capturedEvidenceSources.Value;
        }
        ProjectionCheckpoint? accessPopulationFence = null;
        if (accessPopulationConsistency is not null)
        {
            var capturedPopulations = await accessPopulationConsistency.CaptureAsync(
                request.TenantId, ct).ConfigureAwait(false);
            if (!capturedPopulations.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(capturedPopulations.Error);
            accessPopulationFence = capturedPopulations.Value;
        }
        ProjectionCheckpoint? reviewCampaignFence = null;
        if (reviewCampaignConsistency is not null)
        {
            var capturedCampaigns = await reviewCampaignConsistency.CaptureAsync(
                request.TenantId, ct).ConfigureAwait(false);
            if (!capturedCampaigns.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(capturedCampaigns.Error);
            reviewCampaignFence = capturedCampaigns.Value;
        }
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

        var systemInstanceBoundaryReferences =
            new List<ApplicationBoundaryReferenceView>();
        var systemInstanceBoundaryReferencesOverLimit = false;
        if (instanceConsistency is not null)
        {
            systemInstanceBoundaryReferencesOverLimit = instancePage.NextCursor is not null;
            foreach (var instance in instancePage.Items)
            {
                var remaining = MaximumSystemInstanceBoundaryReferences -
                    systemInstanceBoundaryReferences.Count;
                if (remaining == 0)
                {
                    systemInstanceBoundaryReferencesOverLimit = true;
                    break;
                }

                var instanceBoundaries = await references.ListAsync(request.TenantId,
                    "system_instance", instance.SystemInstanceId,
                    Math.Min(200, remaining), null, ct).ConfigureAwait(false);
                if (instanceBoundaries.Items.Count > remaining ||
                    instanceBoundaries.Items.Any(reference =>
                        !IsValidSystemInstanceBoundaryReference(reference, request.TenantId,
                            instance.SystemInstanceId)))
                    return Result<ApplicationChangePreview>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The system-instance boundary reference projection is inconsistent.",
                        isTransient: true));

                systemInstanceBoundaryReferences.AddRange(instanceBoundaries.Items);
                if (instanceBoundaries.NextCursor is not null)
                {
                    systemInstanceBoundaryReferencesOverLimit = true;
                    break;
                }
            }
        }

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

        var approvedControlReferences = new List<ApplicationControlVersionReferenceView>();
        var approvedApplicationReferencesOverLimit = false;
        var approvedSystemInstanceReferencesOverLimit = instancePage.NextCursor is not null;
        if (controlVersions is not null)
        {
            var applicationControlVersions = await controlVersions.ListAsync(request.TenantId,
                "application", request.ApplicationId, MaximumApplicationControlVersionReferences,
                null, ct).ConfigureAwait(false);
            if (applicationControlVersions.Items.Count > MaximumApplicationControlVersionReferences ||
                applicationControlVersions.Items.Any(reference =>
                    !IsValidApprovedControlReference(reference, request.TenantId, "application",
                        request.ApplicationId)))
                return Result<ApplicationChangePreview>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The approved application control-version projection is inconsistent.",
                    isTransient: true));
            approvedApplicationReferencesOverLimit =
                applicationControlVersions.NextCursor is not null;
            approvedControlReferences.AddRange(applicationControlVersions.Items);

            if (instanceConsistency is not null)
            {
                var systemInstanceReferenceCount = 0;
                foreach (var instance in instancePage.Items)
                {
                    var remaining = MaximumSystemInstanceControlVersionReferences -
                        systemInstanceReferenceCount;
                    if (remaining == 0)
                    {
                        approvedSystemInstanceReferencesOverLimit = true;
                        break;
                    }

                    var instanceControlVersions = await controlVersions.ListAsync(request.TenantId,
                        "system_instance", instance.SystemInstanceId, Math.Min(200, remaining),
                        null, ct).ConfigureAwait(false);
                    if (instanceControlVersions.Items.Count > remaining ||
                        instanceControlVersions.Items.Any(reference =>
                            !IsValidApprovedControlReference(reference, request.TenantId,
                                "system_instance", instance.SystemInstanceId)))
                        return Result<ApplicationChangePreview>.Failure(new RequestError(
                            RequestErrorKind.Conflict,
                            "The approved system-instance control-version projection is inconsistent.",
                            isTransient: true));

                    approvedControlReferences.AddRange(instanceControlVersions.Items);
                    systemInstanceReferenceCount += instanceControlVersions.Items.Count;
                    if (instanceControlVersions.NextCursor is not null)
                    {
                        approvedSystemInstanceReferencesOverLimit = true;
                        break;
                    }
                }
            }
        }

        var policyReferenceItems = new List<ApplicationPolicyReferenceView>();
        var policyReferencesOverLimit = instancePage.NextCursor is not null;
        var policyHasUnresolvedReferences = false;
        if (policyReferences is not null)
        {
            var applicationPolicyReferences = await policyReferences.ListAsync(request.TenantId,
                "application", request.ApplicationId, MaximumApplicationPolicyReferences, null, ct)
                .ConfigureAwait(false);
            if (applicationPolicyReferences.Items.Count > MaximumApplicationPolicyReferences ||
                applicationPolicyReferences.Items.Any(reference =>
                    !IsValidPolicyReference(reference, request.TenantId, "application",
                        request.ApplicationId)))
                return Result<ApplicationChangePreview>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The application policy-reference projection is inconsistent.",
                    isTransient: true));
            policyReferencesOverLimit |= applicationPolicyReferences.NextCursor is not null;
            policyReferenceItems.AddRange(applicationPolicyReferences.Items);

            var systemInstancePolicyReferenceCount = 0;
            if (instanceConsistency is not null)
            {
                foreach (var instance in instancePage.Items)
                {
                    var remaining = MaximumSystemInstancePolicyReferences -
                        systemInstancePolicyReferenceCount;
                    if (remaining == 0)
                    {
                        policyReferencesOverLimit = true;
                        break;
                    }

                    var instancePolicyReferences = await policyReferences.ListAsync(
                        request.TenantId, "system_instance", instance.SystemInstanceId,
                        Math.Min(MaximumSystemInstancePolicyReferences, remaining), null, ct)
                        .ConfigureAwait(false);
                    if (instancePolicyReferences.Items.Count > remaining ||
                        instancePolicyReferences.Items.Any(reference =>
                            !IsValidPolicyReference(reference, request.TenantId,
                                "system_instance", instance.SystemInstanceId)))
                        return Result<ApplicationChangePreview>.Failure(new RequestError(
                            RequestErrorKind.Conflict,
                            "The system-instance policy-reference projection is inconsistent.",
                            isTransient: true));

                    policyReferenceItems.AddRange(instancePolicyReferences.Items);
                    systemInstancePolicyReferenceCount += instancePolicyReferences.Items.Count;
                    if (instancePolicyReferences.NextCursor is not null)
                    {
                        policyReferencesOverLimit = true;
                        break;
                    }
                }
            }
            policyHasUnresolvedReferences = await policyReferences.HasUnresolvedReferencesAsync(
                request.TenantId, ct).ConfigureAwait(false);
        }

        var evidenceSourceReferenceItems = new List<ApplicationEvidenceSourceReferenceView>();
        var evidenceSourceReferencesOverLimit = false;
        var evidenceSourceHasUnresolvedReferences = false;
        var hasHiddenEvidenceSourceReferences = false;
        var evidenceSourceControlsIncomplete = controlPage.NextCursor is not null ||
            systemInstanceControlDraftReferencesOverLimit ||
            approvedApplicationReferencesOverLimit || approvedSystemInstanceReferencesOverLimit ||
            controlVersions is null || controlVersionConsistency is null ||
            instanceConsistency is null || instancePage.NextCursor is not null;
        var canDiscoverEvidenceSources = evidenceSourceAccess is not null && userId != Uuid.Empty;
        var affectedControlReferences = controlPage.Items
            .Concat(systemInstanceControlDraftReferences)
            .Select(static reference => (reference.TenantId, reference.ProgramId,
                reference.ControlId))
            .Concat(approvedControlReferences.Select(static reference =>
                (reference.TenantId, reference.ProgramId, reference.ControlId)))
            .ToArray();
        if (affectedControlReferences.Any(reference => reference.TenantId != request.TenantId ||
                reference.ProgramId == Uuid.Empty || reference.ControlId == Uuid.Empty))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The application control reference projection is inconsistent.",
                isTransient: true));
        var affectedControlPrograms = affectedControlReferences
            .Distinct()
            .ToArray();
        if (affectedControlPrograms.GroupBy(static reference => reference.ControlId)
            .Any(static group => group.Select(reference => reference.ProgramId).Distinct().Count() > 1))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The application control reference projection is inconsistent.",
                isTransient: true));
        var affectedControls = affectedControlPrograms
            .GroupBy(static reference => reference.ControlId)
            .Select(static group => group.First())
            .OrderBy(static reference => reference.ControlId.ToString(), StringComparer.Ordinal)
            .ToArray();
        var affectedProgramIds = affectedControls
            .Select(static reference => reference.ProgramId)
            .Distinct()
            .ToArray();
        if (evidenceSources is not null)
        {
            evidenceSourceHasUnresolvedReferences = await evidenceSources
                .HasUnresolvedReferencesAsync(request.TenantId, affectedProgramIds, ct)
                .ConfigureAwait(false);
            if (canDiscoverEvidenceSources)
            {
                if (affectedControls.Length > MaximumApplicationEvidenceSourceReferences)
                    evidenceSourceReferencesOverLimit = true;

                var artifactAccess = new Dictionary<Uuid, bool>();
                var scannedEvidenceSourceReferences = 0;
                foreach (var affectedControl in affectedControls.Take(
                             MaximumApplicationEvidenceSourceReferences))
                {
                    var remaining = MaximumApplicationEvidenceSourceReferences -
                        scannedEvidenceSourceReferences;
                    if (remaining == 0)
                    {
                        evidenceSourceReferencesOverLimit = true;
                        break;
                    }

                    var sourcePage = await evidenceSources.ListByControlAsync(request.TenantId,
                        affectedControl.ControlId, remaining, null, ct).ConfigureAwait(false);
                    if (sourcePage.Items.Count > remaining || sourcePage.Items.Any(reference =>
                            !IsValidEvidenceSourceReference(reference, request.TenantId,
                                affectedControl.ControlId, affectedControl.ProgramId)))
                        return Result<ApplicationChangePreview>.Failure(new RequestError(
                            RequestErrorKind.Conflict,
                            "The application evidence-source reference projection is inconsistent.",
                            isTransient: true));

                    scannedEvidenceSourceReferences += sourcePage.Items.Count;
                    evidenceSourceReferencesOverLimit |= sourcePage.NextCursor is not null;
                    foreach (var reference in sourcePage.Items)
                    {
                        if (!artifactAccess.TryGetValue(reference.ArtifactId, out var canRead))
                        {
                            canRead = await evidenceSourceAccess!.CanReadAsync(request.TenantId,
                                userId, reference.ArtifactId, ct).ConfigureAwait(false);
                            artifactAccess.Add(reference.ArtifactId, canRead);
                        }
                        if (!canRead)
                        {
                            hasHiddenEvidenceSourceReferences = true;
                            continue;
                        }
                        evidenceSourceReferenceItems.Add(reference);
                    }

                    if (sourcePage.NextCursor is not null)
                        break;
                }
            }
        }

        var accessPopulationReferenceItems =
            new List<ApplicationAccessPopulationReferenceView>();
        var accessPopulationReferencesOverLimit = instancePage.NextCursor is not null;
        var accessPopulationSourcesIncomplete = instanceConsistency is null ||
            instancePage.NextCursor is not null;
        if (accessPopulations is not null && instanceConsistency is not null)
        {
            var scannedPopulationReferences = 0;
            foreach (var instance in instancePage.Items)
            {
                var remaining = MaximumApplicationAccessPopulationReferences -
                    scannedPopulationReferences;
                if (remaining == 0)
                {
                    accessPopulationReferencesOverLimit = true;
                    break;
                }

                var populationPage = await accessPopulations.ListAsync(request.TenantId,
                    instance.SystemInstanceId, remaining, null, ct).ConfigureAwait(false);
                if (populationPage.Items.Count > remaining || populationPage.Items.Any(population =>
                        !IsValidAccessPopulation(population, request.TenantId, request.ApplicationId,
                            instance.SystemInstanceId, instance.Revision)))
                    return Result<ApplicationChangePreview>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The application access-population projection is inconsistent.",
                        isTransient: true));

                scannedPopulationReferences += populationPage.Items.Count;
                foreach (var population in populationPage.Items)
                    accessPopulationReferenceItems.Add(new ApplicationAccessPopulationReferenceView(
                        population.TenantId, population.ApplicationId, population.SystemInstanceId,
                        population.PopulationId, population.SystemInstanceRevision,
                        population.ObservedAt, population.Revision, population.Status,
                        population.SnapshotId, population.AcceptedAt));
                if (populationPage.NextCursor is not null)
                {
                    accessPopulationReferencesOverLimit = true;
                    break;
                }
            }
        }

        var reviewCampaignReferenceItems = new List<ApplicationReviewCampaignReferenceView>();
        var reviewCampaignReferencesOverLimit = instancePage.NextCursor is not null;
        var reviewCampaignSourcesIncomplete = instanceConsistency is null ||
            instancePage.NextCursor is not null;
        if (reviewCampaigns is not null && instanceConsistency is not null)
        {
            var scannedCampaignReferences = 0;
            var campaignSummaries = new Dictionary<Uuid, AccessReviewCampaignSummaryView>();
            foreach (var instance in instancePage.Items)
            {
                var remaining = MaximumApplicationReviewCampaignReferences -
                    scannedCampaignReferences;
                if (remaining == 0)
                {
                    reviewCampaignReferencesOverLimit = true;
                    break;
                }

                var campaignPage = await reviewCampaigns.ListBySystemInstanceAsync(
                    request.TenantId, instance.SystemInstanceId, remaining, null, ct)
                    .ConfigureAwait(false);
                if (campaignPage.Items.Count > remaining || campaignPage.Items.Any(reference =>
                        reference.TenantId != request.TenantId ||
                        reference.CampaignId == Uuid.Empty ||
                        reference.SystemInstanceId != instance.SystemInstanceId ||
                        reference.LaunchedAt == default))
                    return Result<ApplicationChangePreview>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The application review-campaign projection is inconsistent.",
                        isTransient: true));

                scannedCampaignReferences += campaignPage.Items.Count;
                foreach (var reference in campaignPage.Items)
                {
                    if (!campaignSummaries.TryGetValue(reference.CampaignId,
                            out var campaign))
                    {
                        campaign = await reviewCampaigns.GetCampaignAsync(request.TenantId,
                            reference.CampaignId, ct).ConfigureAwait(false);
                        if (IsValidReviewCampaignSummary(campaign, request.TenantId,
                                reference.CampaignId))
                            campaignSummaries.Add(reference.CampaignId, campaign!);
                    }
                    if (!IsValidReviewCampaignSummary(campaign, request.TenantId,
                            reference.CampaignId) || campaign!.LaunchedAt != reference.LaunchedAt)
                        return Result<ApplicationChangePreview>.Failure(new RequestError(
                            RequestErrorKind.Conflict,
                            "The application review-campaign projection is inconsistent.",
                            isTransient: true));

                    reviewCampaignReferenceItems.Add(new ApplicationReviewCampaignReferenceView(
                        request.TenantId, request.ApplicationId, instance.SystemInstanceId,
                        reference.CampaignId, campaign.Status, campaign.SnapshotId,
                        campaign.LaunchedAt, campaign.FinalSnapshotId, campaign.CompletedAt));
                }
                if (campaignPage.NextCursor is not null)
                {
                    reviewCampaignReferencesOverLimit = true;
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
        if (controlVersionFence is { } capturedControlVersionFence)
        {
            var controlVersionsConfirmed = await controlVersionConsistency!
                .ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
                    capturedControlVersionFence, ct).ConfigureAwait(false);
            if (!controlVersionsConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(controlVersionsConfirmed.Error);
        }
        if (policyReferenceFence is { } capturedPolicyReferenceFence)
        {
            var policyReferencesConfirmed = await policyReferenceConsistency!
                .ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
                    capturedPolicyReferenceFence, ct).ConfigureAwait(false);
            if (!policyReferencesConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(policyReferencesConfirmed.Error);
        }
        if (evidenceSourceFence is { } capturedEvidenceSourceFence)
        {
            var evidenceSourcesConfirmed = await evidenceSourceConsistency!
                .ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
                    capturedEvidenceSourceFence, ct).ConfigureAwait(false);
            if (!evidenceSourcesConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(evidenceSourcesConfirmed.Error);
        }
        if (accessPopulationFence is { } capturedAccessPopulationFence)
        {
            var populationsConfirmed = await accessPopulationConsistency!
                .ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
                    capturedAccessPopulationFence, ct).ConfigureAwait(false);
            if (!populationsConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(populationsConfirmed.Error);
        }
        if (reviewCampaignFence is { } capturedReviewCampaignFence)
        {
            var campaignsConfirmed = await reviewCampaignConsistency!
                .ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
                    capturedReviewCampaignFence, ct).ConfigureAwait(false);
            if (!campaignsConfirmed.IsSuccess)
                return Result<ApplicationChangePreview>.Failure(campaignsConfirmed.Error);
        }

        // Source rechecks catch a pending application or relationship write that raced the reads.
        var latest = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (latest.Revision != source.Revision ||
            latest.SourceRelationshipRevision != relationshipSourceRevision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application or its relationships changed during preview.",
                isTransient: true));

        List<string> pending = [];
        if (page.NextCursor is not null || systemInstanceBoundaryReferencesOverLimit)
            pending.Add("boundary_references_over_limit");
        if (controlPage.NextCursor is not null)
            pending.Add("control_draft_references_over_limit");
        if (instancePage.NextCursor is not null)
            pending.Add("system_instance_references_over_limit");
        if (relationships.HasMore || incomingRelationshipsOverLimit)
            pending.Add("application_relationships_over_limit");
        pending.AddRange(MissingContextsBeforeSystemInstanceControls);
        if (controlVersions is null || controlVersionConsistency is null ||
            instanceConsistency is null || approvedApplicationReferencesOverLimit ||
            approvedSystemInstanceReferencesOverLimit)
            pending.Add("approved_control_versions_and_lifecycle_impact");
        if (approvedApplicationReferencesOverLimit || approvedSystemInstanceReferencesOverLimit)
            pending.Add("approved_control_versions_and_lifecycle_impact_over_limit");
        if (incomingRelationshipDirectory is null)
            pending.Add("application_relationships");
        if (instanceConsistency is null)
            pending.Add("system_instance_boundary_references");
        if (instanceConsistency is null)
            pending.Add("system_instance_control_draft_references");
        else if (systemInstanceControlDraftReferencesOverLimit)
            pending.Add("system_instance_control_draft_references_over_limit");
        if (policyReferences is null || policyReferenceConsistency is null ||
            instanceConsistency is null || instancePage.NextCursor is not null ||
            policyReferencesOverLimit || policyHasUnresolvedReferences)
            pending.Add("policies");
        if (policyReferencesOverLimit)
            pending.Add("policy_references_over_limit");
        if (evidenceSources is null || evidenceSourceConsistency is null ||
            evidenceSourceAccess is null || !canDiscoverEvidenceSources ||
            evidenceSourceControlsIncomplete || evidenceSourceReferencesOverLimit ||
            evidenceSourceHasUnresolvedReferences || hasHiddenEvidenceSourceReferences)
            pending.Add("evidence_sources");
        if (evidenceSourceReferencesOverLimit)
            pending.Add("evidence_source_references_over_limit");
        if (accessPopulations is null || accessPopulationConsistency is null ||
            accessPopulationSourcesIncomplete || accessPopulationReferencesOverLimit)
            pending.Add("access_populations");
        if (accessPopulationReferencesOverLimit)
            pending.Add("access_population_references_over_limit");
        if (reviewCampaigns is null || reviewCampaignConsistency is null ||
            reviewCampaignSourcesIncomplete || reviewCampaignReferencesOverLimit)
            pending.Add("review_campaigns");
        if (reviewCampaignReferencesOverLimit)
            pending.Add("review_campaign_references_over_limit");
        pending.AddRange(MissingContextsAfterSystemInstanceControls);
        var preview = new ApplicationChangePreview(
            request.TenantId, request.ApplicationId, source.Revision, request.ChangeKind,
            Changes(current, content), page.Items.Concat(systemInstanceBoundaryReferences)
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.BoundaryId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            pending, false)
        {
            ApplicationRelationships = relationships.Items,
            IncomingApplicationRelationships = incomingRelationships,
            ControlDraftReferences = controlPage.Items.Concat(systemInstanceControlDraftReferences)
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            SystemInstanceReferences = instancePage.Items,
            ApprovedControlReferences = approvedControlReferences
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            PolicyReferences = policyReferenceItems
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.PolicyId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.Version)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            EvidenceSourceReferences = evidenceSourceReferenceItems
                .OrderBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.SupportedRecordType, StringComparer.Ordinal)
                .ThenBy(static item => item.SupportedRecordRevision)
                .ThenBy(static item => item.EvidenceSupportId.ToString(), StringComparer.Ordinal)
                .ToArray(),
            AccessPopulationReferences = accessPopulationReferenceItems
                .OrderBy(static item => item.SystemInstanceId.ToString(), StringComparer.Ordinal)
                .ThenByDescending(static item => item.ObservedAt)
                .ThenBy(static item => item.PopulationId.ToString(), StringComparer.Ordinal)
                .ToArray(),
            ReviewCampaignReferences = reviewCampaignReferenceItems
                .OrderBy(static item => item.SystemInstanceId.ToString(), StringComparer.Ordinal)
                .ThenByDescending(static item => item.LaunchedAt)
                .ThenBy(static item => item.CampaignId.ToString(), StringComparer.Ordinal)
                .ToArray(),
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

    static bool IsValidApprovedControlReference(
        ApplicationControlVersionReferenceView reference, Uuid tenantId, string subjectType,
        Uuid governedRecordId) =>
        reference.TenantId == tenantId && reference.SubjectType == subjectType &&
        reference.GovernedRecordId == governedRecordId &&
        reference.ProgramId != Uuid.Empty && reference.ControlId != Uuid.Empty &&
        reference.VersionId != Uuid.Empty && reference.EntryId != Uuid.Empty &&
        reference.Revision > 0 && !string.IsNullOrWhiteSpace(reference.Identifier) &&
        reference.Status is "approved" or "superseded" or "retired" &&
        reference.EffectiveFrom != default &&
        (reference.Status == "approved" ? reference.EffectiveUntil is null :
            reference.EffectiveUntil is { } effectiveUntil &&
            effectiveUntil > reference.EffectiveFrom) &&
        (reference.PendingRetirementEffectiveUntil is null ||
            reference.PendingRetirementEffectiveUntil > reference.EffectiveFrom) &&
        (reference.Status == "approved" ||
            (!reference.HasPendingSuccessor &&
             reference.PendingRetirementEffectiveUntil is null)) &&
        !string.IsNullOrWhiteSpace(reference.Subject) &&
        !string.IsNullOrWhiteSpace(reference.Rationale);

    static bool IsValidPolicyReference(ApplicationPolicyReferenceView reference, Uuid tenantId,
        string subjectType, Uuid governedRecordId)
    {
        if (reference.TenantId != tenantId || reference.SubjectType != subjectType ||
            reference.GovernedRecordId != governedRecordId ||
            reference.ProgramId == Uuid.Empty || reference.PolicyId == Uuid.Empty ||
            reference.EntryId == Uuid.Empty || reference.Revision < 1 ||
            string.IsNullOrWhiteSpace(reference.Identifier) ||
            string.IsNullOrWhiteSpace(reference.Title) ||
            string.IsNullOrWhiteSpace(reference.Subject) ||
            reference.Status is not ("draft" or "approved" or "superseded" or "retired"))
            return false;

        if (reference.Status == "draft")
            return reference.Version is null && reference.EffectiveFrom is null &&
                   reference.EffectiveUntil is null && !reference.HasPendingSuccessor &&
                   reference.PendingRetirementEffectiveUntil is null;

        if (reference.Version is not >= 1 ||
            reference.EffectiveFrom is not { } effectiveFrom || effectiveFrom == default ||
            reference.PendingRetirementEffectiveUntil is { } pendingRetirement &&
            pendingRetirement <= effectiveFrom)
            return false;

        return reference.Status switch
        {
            "approved" => reference.EffectiveUntil is null,
            "superseded" or "retired" => reference.EffectiveUntil is { } effectiveUntil &&
                effectiveUntil > effectiveFrom && !reference.HasPendingSuccessor &&
                reference.PendingRetirementEffectiveUntil is null,
            _ => false,
        };
    }

    static bool IsValidEvidenceSourceReference(ApplicationEvidenceSourceReferenceView reference,
        Uuid tenantId, Uuid controlId, Uuid programId) =>
        reference.TenantId == tenantId && reference.ProgramId == programId &&
        reference.EvidenceSupportId != Uuid.Empty && reference.ControlId == controlId &&
        reference.SupportedRecordType is "control" or "control_version" &&
        reference.SupportedRecordRevision > 0 && reference.ArtifactId != Uuid.Empty &&
        reference.ArtifactRevision > 0 && reference.PeriodStart != default &&
        reference.PeriodEnd != default && reference.PeriodStart <= reference.PeriodEnd &&
        !string.IsNullOrWhiteSpace(reference.Purpose) &&
        reference.Purpose == reference.Purpose.Trim() && reference.Purpose.Length <= 2000;

    static bool IsValidAccessPopulation(AccessPopulationSummaryView population, Uuid tenantId,
        Uuid applicationId, Uuid systemInstanceId, long currentSystemInstanceRevision)
    {
        if (population.TenantId != tenantId || population.ApplicationId != applicationId ||
            population.SystemInstanceId != systemInstanceId || population.PopulationId == Uuid.Empty ||
            population.SystemInstanceRevision < 1 || population.ObservedAt == default ||
            population.Revision < 1 ||
            population.SystemInstanceRevision > currentSystemInstanceRevision)
            return false;

        if (population.Status == AccessPopulation.Draft)
            return population.SnapshotId is null && population.ContentSha256 is null &&
                   population.AcceptedAt is null;
        return population.Status == AccessPopulation.Accepted &&
            population.SnapshotId is { } snapshotId && snapshotId != Uuid.Empty &&
            population.ContentSha256 is { Length: 64 } contentSha256 &&
            contentSha256.All(Uri.IsHexDigit) && population.AcceptedAt is { } acceptedAt &&
            acceptedAt != default && acceptedAt >= population.ObservedAt;
    }

    static bool IsValidReviewCampaignSummary(AccessReviewCampaignSummaryView? campaign,
        Uuid tenantId, Uuid campaignId)
    {
        if (campaign is null || campaign.TenantId != tenantId ||
            campaign.CampaignId != campaignId || campaign.SnapshotId == Uuid.Empty ||
            campaign.LaunchedAt == default || campaign.ItemCount < 1 ||
            string.IsNullOrWhiteSpace(campaign.Name))
            return false;

        return campaign.Status switch
        {
            AccessReviewCampaign.Active => campaign.FinalSnapshotId is null &&
                campaign.CompletedAt is null,
            AccessReviewCampaign.Completed => campaign.FinalSnapshotId is { } finalSnapshotId &&
                finalSnapshotId != Uuid.Empty && campaign.CompletedAt is { } completedAt &&
                completedAt != default && completedAt >= campaign.LaunchedAt,
            _ => false,
        };
    }

    static bool IsValidSystemInstanceBoundaryReference(
        ApplicationBoundaryReferenceView reference, Uuid tenantId, Uuid systemInstanceId) =>
        reference.TenantId == tenantId && reference.SubjectType == "system_instance" &&
        reference.GovernedRecordId == systemInstanceId && reference.BoundaryId != Uuid.Empty &&
        reference.ProgramId != Uuid.Empty && reference.VersionId != Uuid.Empty &&
        reference.EntryId != Uuid.Empty && reference.Revision > 0 &&
        reference.Status is "draft" or "approved" or "historical" &&
        !string.IsNullOrWhiteSpace(reference.Kind) &&
        !string.IsNullOrWhiteSpace(reference.Subject) &&
        !string.IsNullOrWhiteSpace(reference.OwnerReference) &&
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
