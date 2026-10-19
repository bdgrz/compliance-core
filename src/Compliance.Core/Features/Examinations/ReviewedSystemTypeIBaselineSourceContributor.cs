using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures the reviewed-system revisions included by the exact approved Type I boundary.</summary>
public sealed class ReviewedSystemTypeIBaselineSourceContributor(
    IApplicationDirectoryReader applications, IAggregateReader reader,
    IDomainEventReader events, SystemInstanceReadConsistency consistency,
    RestrictedApplicationVisibility visibility, IBoundaryDirectoryReader boundaries,
    ProgramSetupWorkReadConsistency boundaryConsistency) : ITypeIBaselineSourceContributor
{
    const string RevisionDigestDomain = "bdgrz.snapshot.source.reviewed_system_revision.v1\0";
    const string RetirementDigestDomain = "bdgrz.snapshot.source.reviewed_system_retirement.v1\0";

    public string Family => "reviewed_systems";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A reviewed-system baseline read requires its engagement and source context."));

        var priorBoundaries = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundaries.Length != 1 || priorBoundaries[0].RecordId != engagement.BoundaryId)
            return Success([], ["Reviewed systems were not read from the engagement's exact approved boundary."], []);

        var boundaryFence = await boundaryConsistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!boundaryFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryFence.Error);
        var applicationFence = await consistency.CaptureApplicationListAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!applicationFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(applicationFence.Error);

        var boundary = await boundaries.GetAsync(engagement.TenantId, engagement.BoundaryId, ct)
            .ConfigureAwait(false);
        var boundaryVersion = await boundaries.GetEffectiveVersionAsync(engagement.TenantId,
            engagement.BoundaryId, engagement.PeriodStart, ct).ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != engagement.TenantId ||
            boundary.ProgramId != engagement.ProgramId || boundary.BoundaryId != engagement.BoundaryId ||
            boundaryVersion is null || boundaryVersion.TenantId != engagement.TenantId ||
            boundaryVersion.ProgramId != engagement.ProgramId ||
            boundaryVersion.BoundaryId != engagement.BoundaryId ||
            boundaryVersion.VersionId != priorBoundaries[0].VersionId ||
            boundaryVersion.Revision != priorBoundaries[0].Revision ||
            boundaryVersion.Status != "approved" ||
            boundaryVersion.EffectiveFrom is not { } effectiveFrom ||
            effectiveFrom > engagement.PeriodStart)
            return BoundaryChanged();

        string boundaryDigest;
        try
        {
            boundaryDigest = SnapshotContentIdentity.ApprovedBoundaryVersion(boundaryVersion);
        }
        catch (SnapshotContentException)
        {
            return Success([], ["The reviewed-system scope has invalid approved boundary content."], []);
        }
        if (!StringComparer.Ordinal.Equals(boundaryDigest, priorBoundaries[0].ContentSha256))
            return BoundaryChanged();

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        var instanceIds = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));

        foreach (var entry in boundaryVersion.Content.Entries.Where(static entry =>
                     entry.SubjectType == "system_instance" && entry.Kind == "inclusion"))
        {
            if (entry.Unresolved || entry.GovernedRecordId is not { } instanceId ||
                instanceId == Uuid.Empty)
            {
                blockers.Add("An included reviewed system in the approved boundary cannot be resolved.");
                scopeDifferences.Add($"Boundary scope entry {entry.EntryId} does not identify a resolved reviewed system.");
                continue;
            }
            instanceIds.Add(instanceId);
        }

        foreach (var instanceId in instanceIds)
        {
            var current = await applications.GetInstanceAsync(engagement.TenantId, instanceId, ct)
                .ConfigureAwait(false);
            if (current is null)
            {
                blockers.Add("A reviewed system included by the approved boundary has no source history.");
                continue;
            }
            if (!IsValidDirectoryView(current, engagement.TenantId, instanceId))
                return InvalidDirectory();

            var parentApplicationSources = context.PreviouslyCapturedSources.Where(source =>
                source.Kind == "application_revision" && source.RecordId == current.ApplicationId)
                .ToArray();
            if (parentApplicationSources.Length != 1)
            {
                blockers.Add("A reviewed system's parent application was not captured by the Type I baseline.");
                continue;
            }
            var parentApplicationSource = parentApplicationSources[0];
            if (parentApplicationSource.Revision < 1 ||
                parentApplicationSource.VersionId != Uuid.CreateVersion5(current.ApplicationId,
                    $"application_revision_v1:{parentApplicationSource.Revision}") ||
                !IsSha256(parentApplicationSource.ContentSha256) ||
                current.LegacyApplicationRevision is { } legacyParentRevision &&
                parentApplicationSource.Revision < legacyParentRevision)
            {
                blockers.Add("A reviewed system's parent application source reference is invalid or incomplete.");
                continue;
            }

            var readable = await visibility.CanReadSystemInstanceAsync(engagement.TenantId,
                context.RequesterUserId, current.ApplicationId, instanceId, ct).ConfigureAwait(false);
            if (!readable)
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                    RequestErrorKind.Forbidden,
                    "The requester is not authorized to read every restricted reviewed system in the Type I boundary."));

            var consistent = await consistency.EnsureAsync(engagement.TenantId,
                current.ApplicationId, null, instanceId, null, ct).ConfigureAwait(false);
            if (!consistent.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(consistent.Error);

            var aggregate = await reader.HydrateAsync(new DeclaredSystemInstance(
                engagement.TenantId, instanceId), ct).ConfigureAwait(false);
            var history = aggregate.IsCreated
                ? await ReadModernHistoryAsync(engagement.TenantId, current.ApplicationId,
                    instanceId, ct).ConfigureAwait(false)
                : await ReadLegacyHistoryAsync(engagement.TenantId, current, ct)
                    .ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);

            if (aggregate.IsCreated)
            {
                if (aggregate.Id != instanceId || aggregate.ApplicationId != current.ApplicationId ||
                    aggregate.Revision != current.Revision || history.Value.Count != aggregate.Revision ||
                    !MatchesCurrentAggregate(current, aggregate, history.Value))
                    return SourceConflict();
            }
            else if (history.Value.Count != 1 || current.Revision != 1 ||
                     current.LegacyApplicationRevision is null || current.Lifecycle != "active" ||
                     current.Retirement is not null)
            {
                return SourceConflict();
            }

            if (current.Unresolved.Contains("declaration_conflict", StringComparer.Ordinal))
            {
                blockers.Add("A reviewed system included by the approved boundary has a conflicting identity declaration.");
                scopeDifferences.Add($"Reviewed system {instanceId} has a conflicting identity declaration.");
                continue;
            }

            var latestAsOf = history.Value.Where(state =>
                    state.ChangeKind != "retired" && state.ChangedAt <= through)
                .OrderBy(static state => state.Revision).LastOrDefault();
            if (latestAsOf is null)
            {
                blockers.Add("A reviewed system included by the approved boundary has no version recorded by the Type I date.");
                continue;
            }

            if (latestAsOf.AccessBoundaryReference is null)
                blockers.Add("A reviewed system included by the approved boundary has no access boundary on the Type I date.");
            if (latestAsOf.SourceIdentifier is null)
                blockers.Add("A reviewed system included by the approved boundary has no source identifier on the Type I date.");
            foreach (var unresolved in UnresolvedFields(latestAsOf))
                scopeDifferences.Add($"Reviewed system {instanceId} has unresolved source field {unresolved} on the Type I date.");

            try
            {
                sources.Add(new TypeIBaselineSourceReference("reviewed_system_revision", instanceId,
                    RevisionVersionId(instanceId, latestAsOf.Revision), latestAsOf.Revision,
                    RevisionDigest(engagement.TenantId, current.ApplicationId, instanceId,
                        latestAsOf)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add("A reviewed system has invalid canonical source content.");
                scopeDifferences.Add($"Reviewed system {instanceId} has invalid canonical source content.");
            }

            var retirement = history.Value.SingleOrDefault(static state => state.ChangeKind == "retired");
            if (retirement is null || retirement.ChangedAt > through && retirement.EffectiveAt > through)
                continue;
            try
            {
                sources.Add(new TypeIBaselineSourceReference("reviewed_system_retirement", instanceId,
                    RetirementVersionId(instanceId, retirement.Revision), retirement.Revision,
                    RetirementDigest(engagement.TenantId, current.ApplicationId, instanceId,
                        retirement)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add("A reviewed system has invalid canonical retirement content.");
                scopeDifferences.Add($"Reviewed system {instanceId} has invalid canonical retirement content.");
            }
            if (retirement.EffectiveAt > through)
                scopeDifferences.Add($"Reviewed system {instanceId} has a retirement scheduled after the Type I date.");
            else if (retirement.ChangedAt > through)
                scopeDifferences.Add($"Reviewed system {instanceId} was retired retroactively after the Type I date.");
        }

        var applicationsConfirmed = await consistency.ConfirmApplicationListUnchangedAndCaughtUpAsync(
            engagement.TenantId, applicationFence.Value, ct).ConfigureAwait(false);
        if (!applicationsConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(applicationsConfirmed.Error);
        var boundaryConfirmed = await boundaryConsistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, boundaryFence.Value, ct).ConfigureAwait(false);
        if (!boundaryConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryConfirmed.Error);

        return Success(sources, blockers, scopeDifferences);
    }

    async ValueTask<Result<IReadOnlyList<ReviewedSystemState>>> ReadModernHistoryAsync(
        Uuid tenantId, Uuid applicationId, Uuid instanceId, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), "system-instances",
            instanceId.ToString());
        var history = new List<ReviewedSystemState>();
        long nextRevision = 1;
        var retired = false;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream)
                return InvalidHistory();
            var state = record.Event switch
            {
                SystemInstanceRegistered registered when !retired &&
                    IsValid(registered, tenantId, applicationId, instanceId, nextRevision) =>
                    new ReviewedSystemState(registered.Revision, registered.Name, registered.Kind,
                        registered.AccessBoundaryReference, registered.SourceIdentifier,
                        registered.ActorMemberId, registered.ActorDisplay, registered.Actor,
                        registered.ChangedAt, "registered", null, null, null),
                SystemInstanceRevised revised when !retired &&
                    IsValid(revised, tenantId, applicationId, instanceId, nextRevision) =>
                    new ReviewedSystemState(revised.Revision, revised.Name, revised.Kind,
                        revised.AccessBoundaryReference, revised.SourceIdentifier,
                        revised.ActorMemberId, revised.ActorDisplay, revised.Actor,
                        revised.ChangedAt, "revised", revised.Rationale, null, null),
                SystemInstanceRetired retirement when !retired &&
                    IsValid(retirement, tenantId, applicationId, instanceId, nextRevision) =>
                    new ReviewedSystemState(retirement.Revision, null, null, null, null,
                        retirement.ActorMemberId, retirement.ActorDisplay, retirement.Actor,
                        retirement.ChangedAt, "retired", retirement.Reason,
                        retirement.EffectiveAt, retirement.Reason),
                _ => null,
            };
            if (state is null || history.Count == 0 && state.ChangeKind != "registered" ||
                history.Count > 0 && state.ChangeKind == "registered")
                return InvalidHistory();
            history.Add(state);
            nextRevision++;
            retired = state.ChangeKind == "retired";
        }
        return history.Count == 0
            ? InvalidHistory()
            : Result<IReadOnlyList<ReviewedSystemState>>.Success(history.AsReadOnly());
    }

    async ValueTask<Result<IReadOnlyList<ReviewedSystemState>>> ReadLegacyHistoryAsync(
        Uuid tenantId, SystemInstanceView current, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), "applications",
            current.ApplicationId.ToString());
        var declarations = new List<SystemInstanceDeclared>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream)
                return InvalidHistory();
            if (record.Event is SystemInstanceDeclared candidate &&
                candidate.SystemInstanceId == current.SystemInstanceId)
                declarations.Add(candidate);
        }
        if (declarations.Count != 1)
            return InvalidHistory();
        var declaration = declarations[0];
        if (!IsValid(declaration, tenantId, current.ApplicationId,
                current.SystemInstanceId) || declaration.ApplicationRevision != current.LegacyApplicationRevision ||
            !StringComparer.Ordinal.Equals(declaration.Name, current.Name) ||
            !StringComparer.Ordinal.Equals(declaration.Kind, current.Kind) ||
            !StringComparer.Ordinal.Equals(declaration.AccessBoundaryReference,
                current.AccessBoundaryReference) ||
            !StringComparer.Ordinal.Equals(declaration.SourceIdentifier, current.SourceIdentifier) ||
            declaration.ActorMemberId != current.DeclaredByMemberId ||
            declaration.Actor != current.DeclaredBy || declaration.ChangedAt != current.DeclaredAt)
            return InvalidHistory();
        return Result<IReadOnlyList<ReviewedSystemState>>.Success(
            [new ReviewedSystemState(1, declaration.Name, declaration.Kind,
                declaration.AccessBoundaryReference, declaration.SourceIdentifier,
                declaration.ActorMemberId, declaration.ActorDisplay, declaration.Actor,
                declaration.ChangedAt, "legacy_declared", null, null, null,
                declaration.ApplicationRevision)]);
    }

    static bool MatchesCurrentAggregate(SystemInstanceView current,
        DeclaredSystemInstance aggregate, IReadOnlyList<ReviewedSystemState> history)
    {
        var registration = history[0];
        var latestState = history.LastOrDefault(static state => state.ChangeKind != "retired");
        var retirement = history.SingleOrDefault(static state => state.ChangeKind == "retired");
        return latestState is not null && current.LegacyApplicationRevision is null &&
               aggregate.IsCreated && aggregate.Revision == current.Revision &&
               aggregate.IsRetired == (retirement is not null) &&
               aggregate.Name == latestState.Name && aggregate.Kind == latestState.Kind &&
               aggregate.AccessBoundaryReference == latestState.AccessBoundaryReference &&
               aggregate.SourceIdentifier == latestState.SourceIdentifier &&
               current.Name == latestState.Name && current.Kind == latestState.Kind &&
               current.AccessBoundaryReference == latestState.AccessBoundaryReference &&
               current.SourceIdentifier == latestState.SourceIdentifier &&
               current.DeclaredByMemberId == registration.ActorMemberId &&
               current.DeclaredBy == registration.Actor &&
               current.Lifecycle == (retirement is null ? "active" : "retired") &&
               (retirement is null ? current.Retirement is null :
                   current.Retirement is { } retired && retired.EffectiveAt == retirement.EffectiveAt &&
                   retired.Reason == retirement.RetirementReason);
    }

    static bool IsValidDirectoryView(SystemInstanceView view, Uuid tenantId, Uuid instanceId) =>
        view.TenantId == tenantId && view.SystemInstanceId == instanceId &&
        view.ApplicationId != Uuid.Empty && view.Revision >= 1 &&
        view.LegacyApplicationRevision is not < 1 &&
        !string.IsNullOrWhiteSpace(view.Name) && view.Name == view.Name.Trim() &&
        !string.IsNullOrWhiteSpace(view.Kind) && view.Kind == view.Kind.Trim() &&
        ValidText(view.SourceKind, 200) && OptionalText(view.SourceIdentifier, 1000) &&
        OptionalText(view.AccessBoundaryReference, 2000) &&
        view.Unresolved is not null && !view.Unresolved.Any(static gap => gap is null) &&
        view.DeclaredByMemberId != Uuid.Empty && ValidText(view.DeclaredByDisplay, 200) &&
        view.DeclaredAt != default && view.Lifecycle is "active" or "retired" &&
        (view.Lifecycle == "active" ? view.Retirement is null :
            view.Retirement is { } retirement && retirement.EffectiveAt != default &&
            ValidText(retirement.Reason, 2000));

    static bool IsValid(SystemInstanceRegistered ev, Uuid tenantId, Uuid applicationId,
        Uuid instanceId, long revision) =>
        ev.TenantId == tenantId && ev.ApplicationId == applicationId &&
        ev.SystemInstanceId == instanceId && ev.Revision == revision &&
        ValidText(ev.Name, 200) && ValidText(ev.Kind, 200) &&
        OptionalText(ev.AccessBoundaryReference, 2000) &&
        OptionalText(ev.SourceIdentifier, 1000) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.ChangedAt);

    static bool IsValid(SystemInstanceRevised ev, Uuid tenantId, Uuid applicationId,
        Uuid instanceId, long revision) =>
        ev.TenantId == tenantId && ev.ApplicationId == applicationId &&
        ev.SystemInstanceId == instanceId && ev.Revision == revision &&
        ValidText(ev.Name, 200) && ValidText(ev.Kind, 200) &&
        OptionalText(ev.AccessBoundaryReference, 2000) &&
        OptionalText(ev.SourceIdentifier, 1000) &&
        ValidText(ev.Rationale, 2000) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.ChangedAt);

    static bool IsValid(SystemInstanceRetired ev, Uuid tenantId, Uuid applicationId,
        Uuid instanceId, long revision) =>
        ev.TenantId == tenantId && ev.ApplicationId == applicationId &&
        ev.SystemInstanceId == instanceId && ev.Revision == revision && ev.EffectiveAt != default &&
        ValidText(ev.Reason, 2000) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.ChangedAt);

    static bool IsValid(SystemInstanceDeclared ev, Uuid tenantId, Uuid applicationId,
        Uuid instanceId) =>
        ev.TenantId == tenantId && ev.ApplicationId == applicationId &&
        ev.SystemInstanceId == instanceId && ev.ApplicationRevision >= 1 &&
        ValidText(ev.Name, 200) && ValidText(ev.Kind, 200) &&
        OptionalText(ev.AccessBoundaryReference, 2000) &&
        OptionalText(ev.SourceIdentifier, 1000) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.ChangedAt);

    static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();

    static bool OptionalText(string? value, int maximum) =>
        value is null || ValidText(value, maximum);

    static bool ValidActor(Uuid memberId, string display, ActorReference actor,
        DateTimeOffset changedAt) =>
        memberId != Uuid.Empty && ValidText(display, 200) && changedAt != default &&
        actor == ActorReference.ForMember(memberId, display);

    static IEnumerable<string> UnresolvedFields(ReviewedSystemState state)
    {
        if (state.AccessBoundaryReference is null)
            yield return "access_boundary_missing";
        else
            yield return "access_boundary_unverified";
        if (state.SourceIdentifier is null)
            yield return "source_identifier_missing";
        else
            yield return "source_identifier_unverified";
    }

    static Uuid RevisionVersionId(Uuid instanceId, long revision) =>
        Uuid.CreateVersion5(instanceId, $"reviewed_system_revision_v1:{revision}");

    static Uuid RetirementVersionId(Uuid instanceId, long revision) =>
        Uuid.CreateVersion5(instanceId, $"reviewed_system_retirement_v1:{revision}");

    static string RevisionDigest(Uuid tenantId, Uuid applicationId, Uuid instanceId,
        ReviewedSystemState state)
    {
        var content = new ReviewedSystemRevisionSourceContent(tenantId, applicationId,
            instanceId, state.Revision, state.LegacyApplicationRevision, state.ChangeKind,
            state.Name!, state.Kind!, state.AccessBoundaryReference, state.SourceIdentifier,
            state.Rationale, state.ActorMemberId, state.ActorDisplay, state.Actor,
            state.ChangedAt);
        return Digest(RevisionDigestDomain, content,
            ComplianceCoreJsonContext.Default.ReviewedSystemRevisionSourceContent);
    }

    static string RetirementDigest(Uuid tenantId, Uuid applicationId, Uuid instanceId,
        ReviewedSystemState state)
    {
        var content = new ReviewedSystemRetirementSourceContent(tenantId, applicationId,
            instanceId, state.Revision, state.EffectiveAt!.Value, state.RetirementReason!,
            state.ActorMemberId, state.ActorDisplay, state.Actor, state.ChangedAt);
        return Digest(RetirementDigestDomain, content,
            ComplianceCoreJsonContext.Default.ReviewedSystemRetirementSourceContent);
    }

    static string Digest<T>(string domain, T content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(content, typeInfo);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(domain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    static bool IsSha256(string value) => value.Length == 64 &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<IReadOnlyList<ReviewedSystemState>> InvalidHistory() =>
        Result<IReadOnlyList<ReviewedSystemState>>.Failure(new RequestError(
            RequestErrorKind.Conflict,
            "The reviewed-system source history is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BoundaryChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The approved boundary changed while reviewed-system scope was being read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The reviewed-system directory returned invalid baseline source data."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The reviewed-system source or projection changed during the baseline read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Success(
        IReadOnlyList<TypeIBaselineSourceReference> sources,
        IReadOnlyList<string> blockers, IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            "reviewed_systems", sources, blockers, scopeDifferences));

    sealed record ReviewedSystemState(long Revision, string? Name, string? Kind,
        string? AccessBoundaryReference, string? SourceIdentifier, Uuid ActorMemberId,
        string ActorDisplay, ActorReference Actor, DateTimeOffset ChangedAt, string ChangeKind,
        string? Rationale, DateTimeOffset? EffectiveAt, string? RetirementReason,
        long? LegacyApplicationRevision = null);
}
