using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Derives assigned boundary review and approval work from current draft decisions.</summary>
static class BoundaryDecisionWork
{
    public const string Review = "boundary_review";
    public const string Approval = "boundary_approval";
    public static IReadOnlyList<string> ProjectedKinds { get; } = Array.AsReadOnly<string>(
        [Review, Approval]);

    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        BoundaryReader sources, IBoundaryDirectoryReader directory, Uuid tenantId,
        Uuid programId, DateTimeOffset now, Uuid? workItemId, CancellationToken ct)
    {
        try
        {
            var candidates = new List<WorkCandidate>();
            string? cursor = null;
            do
            {
                var page = await directory.ListProgramAsync(tenantId, programId, 200, cursor, ct)
                    .ConfigureAwait(false);
                foreach (var boundary in page.Items.Where(boundary => boundary.TenantId == tenantId &&
                             boundary.ProgramId == programId))
                {
                    var aggregate = await sources.ReadAsync(tenantId, programId, boundary.BoundaryId, ct).ConfigureAwait(false);
                    if (!aggregate.IsCreated || aggregate.ProgramId != programId)
                        continue;
                    if (aggregate.Revision != boundary.Revision)
                        return Result<IReadOnlyList<WorkCandidate>>.Failure(new RequestError(
                            RequestErrorKind.Conflict,
                            "The boundary source and work projection revisions differ. Retry the query.",
                            isTransient: true));

                    var draft = boundary.Draft;
                    if (draft is null || draft.TenantId != tenantId ||
                        draft.BoundaryId != boundary.BoundaryId || draft.ProgramId != programId ||
                        draft.Status != "draft" || aggregate.DraftVersionId != draft.VersionId ||
                        aggregate.DraftRevision != draft.Revision)
                        continue;

                    var decision = boundary.LatestDecision;
                    if (decision is not null && (decision.TenantId != tenantId ||
                        decision.BoundaryId != boundary.BoundaryId || decision.VersionId != draft.VersionId ||
                        decision.Revision != draft.Revision))
                        continue;
                    var scope = new ResponsibilityScope("boundary", boundary.BoundaryId,
                        draft.VersionId, draft.Revision);
                    var state = new BoundaryDecisionWorkState(tenantId, programId,
                        boundary.BoundaryId, aggregate.Revision, draft.VersionId, draft.Revision,
                        aggregate.DraftAuthorMemberId, aggregate.DraftChangedAt!.Value, aggregate.LatestReviewOutcome,
                        aggregate.GetResponsibilitySet(scope).ReadAssignments().ToArray());
                    var work = Candidates(state, now, workItemId);
                    if (!work.IsSuccess)
                        return Result<IReadOnlyList<WorkCandidate>>.Failure(work.Error);
                    candidates.AddRange(work.Value);
                }
                cursor = page.NextCursor;
            } while (cursor is not null);

            return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<IReadOnlyList<WorkCandidate>>.Failure(BoundaryReadFailure.Of(failure));
        }
    }

    internal static Result<IReadOnlyList<WorkCandidate>> Candidates(
        BoundaryDecisionWorkState state, DateTimeOffset now, Uuid? workItemId)
    {
        if (state.TenantId == Uuid.Empty || state.ProgramId == Uuid.Empty ||
            state.BoundaryId == Uuid.Empty || state.SourceRevision < 1 ||
            state.DraftVersionId == Uuid.Empty || state.DraftRevision < 1 ||
            state.DraftAuthorMemberId == Uuid.Empty ||
            state.Assignments is null || state.LatestReviewOutcome is not
                (null or "accept" or "request_changes"))
            return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

        if (state.LatestReviewOutcome == "request_changes")
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);

        var kind = state.LatestReviewOutcome == "accept" ? Approval : Review;
        var responsibilityType = state.LatestReviewOutcome == "accept"
            ? ResponsibilityType.PolicyApprover
            : ResponsibilityType.AssignedReviewer;
        var nextAction = state.LatestReviewOutcome == "accept" ? "approve" : "review";
        var route = state.LatestReviewOutcome == "accept" ? "approvals" : "reviews";
        var scope = new ResponsibilityScope("boundary", state.BoundaryId,
            state.DraftVersionId, state.DraftRevision);
        var assignments = new HashSet<Uuid>();
        var workItemIds = new HashSet<Uuid>();
        var candidates = new List<WorkCandidate>();
        var prefix = $"/api/v1/tenants/{state.TenantId}/boundaries/{state.BoundaryId}/" +
                     $"drafts/{state.DraftVersionId}/{route}";

        foreach (var assignment in state.Assignments)
        {
            if (assignment is null || assignment.TenantId != state.TenantId ||
                assignment.AssignmentId == Uuid.Empty || assignment.MemberId == Uuid.Empty ||
                assignment.Scope != scope || !Enum.IsDefined(assignment.Type) ||
                assignment.EffectiveUntil is { } until && until <= assignment.EffectiveFrom ||
                !assignments.Add(assignment.AssignmentId))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

            if (assignment.Type != responsibilityType ||
                assignment.EffectiveFrom > now ||
                assignment.EffectiveUntil is { } endsAt && now >= endsAt ||
                assignment.RevokedAt is { } revokedAt && now >= revokedAt ||
                assignment.MemberId == state.DraftAuthorMemberId)
                continue;

            var identity = Uuid.CreateVersion5(state.DraftVersionId,
                $"{kind}\n{state.DraftRevision}\n{assignment.MemberId}");
            var id = WorkCandidate.IdFor(identity, kind);
            if ((workItemId is { } wanted && id != wanted) || !workItemIds.Add(id))
                continue;
            candidates.Add(new WorkCandidate(id, kind, state.DraftVersionId, null, null,
                nextAction == "review" ? "Review system boundary" : "Approve system boundary",
                $"Boundary draft revision {state.DraftRevision} is awaiting an assigned {nextAction}.",
                null, null, nextAction, prefix,
                new OperatingHolder(OperatingAuthority.MemberHolder, assignment.MemberId), null,
                new HashSet<Uuid>(), state.DraftChangedAt)
            {
                RequiredManagementProgramId = state.ProgramId,
            });
        }

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The boundary work projection has an invalid program scope.");
}
