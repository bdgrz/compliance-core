using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Aggregates;

/// <summary>One engagement preservation hold with a report-issued-plus-one-year expiry.</summary>
public sealed class ArtifactEngagementHold : Aggregate
{
    const int MaximumTextLength = 2000;
    const int MaximumReferenceLength = 500;

    readonly Uuid _tenantId;
    ArtifactEngagementHoldView? _current;

    public long Revision => _current?.Revision ?? 0;

    public ArtifactEngagementHoldView? Current => _current;

    public ArtifactEngagementHold(Uuid tenantId, Uuid holdId)
        : base(holdId, new EventStreamAddress(tenantId.ToString(), "artifact-engagement-holds",
            holdId.ToString()))
    {
        _tenantId = tenantId;
        On<ArtifactEngagementHoldPlaced>(Apply);
        On<EngagementReportIssued>(Apply);
        On<ArtifactEngagementHoldReleased>(Apply);
    }

    public Result Place(Uuid engagementId, Uuid? reportArtifactId, string scope, string authority,
        string reason, ActorReference placedBy, DateTimeOffset placedAt)
    {
        scope = Clean(scope);
        authority = Clean(authority);
        reason = Clean(reason);
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || engagementId == Uuid.Empty ||
            reportArtifactId == Uuid.Empty || !ValidMember(placedBy) || placedAt == default ||
            !Bounded(scope, 1, MaximumTextLength) || !Bounded(authority, 1, MaximumTextLength) ||
            !Bounded(reason, 1, MaximumTextLength))
            return Invalid("An engagement hold needs valid scope, authority, reason, and personal attribution.");
        if (_current is { } current)
            return current.EngagementId == engagementId && current.ReportArtifactId == reportArtifactId &&
                   current.Scope == scope && current.Authority == authority && current.Reason == reason &&
                   current.PlacedBy == placedBy && current.PlacedAt == placedAt
                ? Result.Success
                : Conflict("An engagement hold identity cannot be reused with changed scope or authority.");

        var state = new ArtifactEngagementHoldView(_tenantId, Id, engagementId, reportArtifactId,
            scope, authority, reason, "active", 1, placedBy, placedAt, null, null, null, null,
            null, null, null, null, placedAt);
        RaiseEvent(new ArtifactEngagementHoldPlaced(_tenantId, Id, 1, MemberId(placedBy), state,
            placedAt));
        return Result.Success;
    }

    public Result RecordReportIssued(Uuid engagementId, Uuid reportArtifactId,
        string issuerReference, ActorReference issuedBy, DateTimeOffset issuedAt)
    {
        if (_current is not { } current)
            return Missing();
        issuerReference = Clean(issuerReference);
        if (engagementId != current.EngagementId || reportArtifactId == Uuid.Empty ||
            !ValidMember(issuedBy) || !Bounded(issuerReference, 1, MaximumReferenceLength) ||
            issuedAt < current.PlacedAt ||
            issuedAt == default || issuedAt.Year > 9992)
            return Invalid("Report issuance must match this held engagement with an exact artifact and reference.");
        if (current.ReportIssuedAt is not null)
            return current.ReportArtifactId == reportArtifactId &&
                   current.IssuerReference == issuerReference && current.ReportIssuedBy == issuedBy &&
                   current.ReportIssuedAt == issuedAt
                ? Result.Success
                : Conflict("An engagement hold can retain only one exact issued report identity.");
        if (current.ReportArtifactId is { } expected && expected != reportArtifactId)
            return Conflict("The issued report must match the artifact identity recorded by the hold.");
        if (current.Status == "released")
            return Conflict("A released engagement hold cannot be changed.");

        var releaseEligibleOn = DateOnly.FromDateTime(issuedAt.UtcDateTime).AddYears(1);
        var state = current with
        {
            ReportArtifactId = reportArtifactId,
            IssuerReference = issuerReference,
            ReportIssuedBy = issuedBy,
            ReportIssuedAt = issuedAt,
            ReleaseEligibleOn = releaseEligibleOn,
            Revision = Revision + 1,
            UpdatedAt = issuedAt,
        };
        RaiseEvent(new EngagementReportIssued(_tenantId, Id, state.Revision,
            MemberId(issuedBy), state, issuedAt));
        return Result.Success;
    }

    public Result Release(long expectedRevision, string releaseAuthority, string rationale,
        ActorReference releasedBy, DateTimeOffset releasedAt)
    {
        if (_current is not { } current)
            return Missing();
        releaseAuthority = Clean(releaseAuthority);
        rationale = Clean(rationale);
        if (!ValidMember(releasedBy) || releasedAt == default ||
            !Bounded(releaseAuthority, 1, MaximumTextLength) ||
            !Bounded(rationale, 1, MaximumTextLength))
            return Invalid("Engagement hold release requires authority, rationale, and personal attribution.");
        if (current.Status == "released")
            return current.ReleaseAuthority == releaseAuthority && current.ReleaseRationale == rationale &&
                   current.ReleasedBy == releasedBy && current.ReleasedAt == releasedAt &&
                   expectedRevision <= Revision
                ? Result.Success
                : Conflict("An engagement hold release decision is immutable.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.ReleaseEligibleOn is not { } eligibleOn ||
            DateOnly.FromDateTime(releasedAt.UtcDateTime) < eligibleOn)
            return Conflict("An engagement hold remains active until one year after report issuance.");

        var state = current with
        {
            Status = "released",
            Revision = Revision + 1,
            ReleasedBy = releasedBy,
            ReleaseAuthority = releaseAuthority,
            ReleaseRationale = rationale,
            ReleasedAt = releasedAt,
            UpdatedAt = releasedAt,
        };
        RaiseEvent(new ArtifactEngagementHoldReleased(_tenantId, Id, state.Revision,
            MemberId(releasedBy), state, releasedAt));
        return Result.Success;
    }

    public bool IsActive(DateOnly asOf) => _current is { Status: "active" } current &&
        (current.ReleaseEligibleOn is not { } expiry || asOf < expiry);

    void Apply(ArtifactEngagementHoldPlaced ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "placed");

    void Apply(EngagementReportIssued ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "report_issued");

    void Apply(ArtifactEngagementHoldReleased ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, "released");

    void ApplyState(Uuid tenantId, Uuid holdId, long revision, Uuid? actorMemberId,
        ArtifactEngagementHoldView state, DateTimeOffset occurredAt, string transition)
    {
        if (tenantId != _tenantId || holdId != Id || state is null ||
            state.TenantId != _tenantId || state.HoldId != Id || state.Revision != revision ||
            state.UpdatedAt != occurredAt || actorMemberId != TransitionActor(state, transition) ||
            ValidateState(state) is not null ||
            transition == "placed" && (_current is not null || revision != 1 || state.PlacedAt != occurredAt) ||
            transition == "report_issued" && (_current is null || revision != Revision + 1 ||
                !SamePlacedHold(_current, state) || state.ReportIssuedAt != occurredAt ||
                _current.ReportIssuedAt is not null || state.ReportArtifactId != _current.ReportArtifactId &&
                    _current.ReportArtifactId is not null) ||
            transition == "released" && (_current is null || revision != Revision + 1 ||
                !SamePlacedHold(_current, state) || state.ReleasedAt != occurredAt ||
                state.Status != "released" || _current.Status != "active"))
            throw new InvalidOperationException("The artifact engagement hold event is invalid.");
        _current = state with { };
    }

    static RequestError? ValidateState(ArtifactEngagementHoldView state)
    {
        if (state.EngagementId == Uuid.Empty || state.ReportArtifactId == Uuid.Empty ||
            !Bounded(state.Scope, 1, MaximumTextLength) ||
            !Bounded(state.Authority, 1, MaximumTextLength) || !Bounded(state.Reason, 1, MaximumTextLength) ||
            state.Status is not ("active" or "released") || !ValidMember(state.PlacedBy) ||
            state.PlacedAt == default || state.PlacedAt.Year > 9992 ||
            state.ReportIssuedAt is { } issuedAt && (state.ReportArtifactId is null ||
                !ValidMember(state.ReportIssuedBy) ||
                !Bounded(state.IssuerReference, 1, MaximumReferenceLength) || issuedAt < state.PlacedAt ||
                state.ReleaseEligibleOn != DateOnly.FromDateTime(issuedAt.UtcDateTime).AddYears(1)) ||
            state.ReportIssuedAt is null && (state.IssuerReference is not null ||
                state.ReportIssuedBy is not null ||
                state.ReleaseEligibleOn is not null) ||
            state.Status == "released" && (!ValidMember(state.ReleasedBy) ||
                !Bounded(state.ReleaseAuthority, 1, MaximumTextLength) ||
                !Bounded(state.ReleaseRationale, 1, MaximumTextLength) ||
                state.ReleasedAt is null || state.ReleaseEligibleOn is null ||
                DateOnly.FromDateTime(state.ReleasedAt.Value.UtcDateTime) < state.ReleaseEligibleOn) ||
            state.Status == "active" && (state.ReleasedBy is not null || state.ReleaseAuthority is not null ||
                state.ReleaseRationale is not null || state.ReleasedAt is not null))
            return new(RequestErrorKind.Validation, "The retained engagement hold state is invalid.");
        return null;
    }

    static bool SamePlacedHold(ArtifactEngagementHoldView first, ArtifactEngagementHoldView second) =>
        first.TenantId == second.TenantId && first.HoldId == second.HoldId &&
        first.EngagementId == second.EngagementId && first.Scope == second.Scope &&
        first.Authority == second.Authority && first.Reason == second.Reason &&
        first.PlacedBy == second.PlacedBy && first.PlacedAt == second.PlacedAt;

    static Uuid? TransitionActor(ArtifactEngagementHoldView state, string transition) => transition switch
    {
        "placed" => MemberId(state.PlacedBy),
        "report_issued" => state.ReportIssuedBy is null ? null : MemberId(state.ReportIssuedBy),
        "released" => state.ReleasedBy is null ? null : MemberId(state.ReleasedBy),
        _ => null,
    };

    static Uuid? MemberId(ActorReference actor) => Uuid.TryParse(actor.Id, out var id) ? id : null;

    static bool ValidMember(ActorReference? actor) => actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var memberId) && memberId != Uuid.Empty;

    static string Clean(string? text) => (text ?? string.Empty).Trim();

    static bool Bounded(string? text, int minimum, int maximum) => text is not null &&
        text.Trim().Length >= minimum && text.Trim().Length <= maximum;

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The artifact engagement hold was not found."));

    static Result Invalid(string reason) => Result.Failure(new RequestError(RequestErrorKind.Validation, reason));

    static Result Conflict(string reason) => Result.Failure(new RequestError(RequestErrorKind.Conflict, reason));

    Result StaleRevision() => Conflict($"The engagement hold revision is {Revision}.");
}
