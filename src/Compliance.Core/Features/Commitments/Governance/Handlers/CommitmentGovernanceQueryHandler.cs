using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Queries;
using Bdgrz.Compliance.Features.Commitments.Governance.Services;
using Bdgrz.Compliance.Features.Controls.Readers;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Handlers;

public sealed class CommitmentGovernanceQueryHandler(IAggregateReader reader, CommitmentGovernanceAccess access,
    BoundaryReader boundaries, ControlReader controls, TimeProvider clock)
    : IRequestHandler<GetCommitment, CommitmentStatus>, IRequestHandler<GetCommitmentControlLink, CommitmentLinkStatus>
{
    public async ValueTask<Result<CommitmentStatus>> HandleAsync(IRequestContext<GetCommitment> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = await access.RequireAsync(context, request.TenantId, "commitments.read", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<CommitmentStatus>.Failure(actor.Error);
        var owner = await reader.HydrateAsync(new Commitment(request.TenantId, request.CommitmentId), ct).ConfigureAwait(false);
        if (owner.Revision == 0 || owner.ProgramId != request.ProgramId)
            return Result<CommitmentStatus>.Failure(new RequestError(RequestErrorKind.NotFound, "The commitment was not found in this program."));
        var terms = owner.Versions.LastOrDefault()?.Terms ?? owner.OpenDraft;
        IReadOnlyList<string> categories = [];
        if (terms is not null)
        {
            var boundary = await boundaries.ReadAsync(request.TenantId, request.ProgramId, terms.BoundaryId, ct).ConfigureAwait(false);
            categories = terms.Categories.Where(category => boundary.LatestApprovedVersion is not { } current ||
                !current.Content.TrustServicesCategories.Contains(category)).ToArray();
        }
        return Result<CommitmentStatus>.Success(new(request.TenantId, request.ProgramId, request.CommitmentId,
            owner.Kind, owner.Identifier, owner.Revision, owner.OpenDraft, owner.Versions, owner.RetiredOn, categories));
    }
    public async ValueTask<Result<CommitmentLinkStatus>> HandleAsync(IRequestContext<GetCommitmentControlLink> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = await access.RequireAsync(context, request.TenantId, "commitments.read", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<CommitmentLinkStatus>.Failure(actor.Error);
        var owner = await reader.HydrateAsync(new CommitmentControlLink(request.TenantId, request.CommitmentId, request.ControlId), ct).ConfigureAwait(false);
        if (owner.Revision == 0 || owner.ProgramId != request.ProgramId)
            return Result<CommitmentLinkStatus>.Failure(new RequestError(RequestErrorKind.NotFound, "The commitment/control link was not found."));
        var commitment = await reader.HydrateAsync(new Commitment(request.TenantId, request.CommitmentId), ct).ConfigureAwait(false);
        var control = await controls.ReadAsync(request.TenantId, request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var current = commitment.Versions.LastOrDefault(value => value.EffectiveOn <= today);
        var version = control.ReadApprovedVersionHistory().LastOrDefault(value => value.EffectiveFrom <= today &&
            (value.EffectiveUntil is null || value.EffectiveUntil > today));
        var counts = owner.IsLinked && commitment.ProgramId == request.ProgramId && commitment.IsActiveOn(today) &&
            !control.IsRetired && version is not null && current is not null;
        return Result<CommitmentLinkStatus>.Success(new(request.TenantId, request.ProgramId, request.CommitmentId,
            request.ControlId, owner.Revision, owner.IsLinked, owner.CommitmentVersion, owner.ControlVersion,
            owner.Rationale, counts, counts && (current!.Version != owner.CommitmentVersion || version!.Revision != owner.ControlVersion),
            counts && commitment.Kind is "service_commitment" or "system_requirement"));
    }
}
