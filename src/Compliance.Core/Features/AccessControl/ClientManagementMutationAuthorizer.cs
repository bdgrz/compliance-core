using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Evidence;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Adds the permanent Attest write wall; existing membership and resource authorizers still apply.</summary>
sealed class ClientManagementMutationAuthorizer(ClientManagementIndependenceGuard independence,
    ITenantMembershipDirectoryReader memberships,
    ProfessionalAdvisoryReadAccess? professionalAdvisoryRead = null)
    : IRequestAuthorizer<IClientManagementMutationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IClientManagementMutationRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Client management authoring requires a Bdgrz user identity."));
        if (context.Request is CorrectAdvisorFeedback correction && professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanCorrectFeedbackAsync(context.Request.TenantId,
                correction.ProgramId, correction.HandoffId, correction.FeedbackId, userId, ct)
                .ConfigureAwait(false))
            return Result.Success;
        if (context.Request is RecordAdvisorFeedback feedback)
            return professionalAdvisoryRead is not null &&
                   await professionalAdvisoryRead.CanRecordFeedbackAsync(context.Request.TenantId,
                       feedback.ProgramId, feedback.HandoffId,
                       feedback.SelectedSource ?? new AdvisorRecordVersion(feedback.RecordId,
                           feedback.RecordVersionId), userId, ct).ConfigureAwait(false)
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The shared advisor handoff was not found."));
        if (context.Request is IssueArtifactDownload download &&
            download.ProgramId is { } programId && download.AdvisorHandoffId is { } handoffId &&
            download.SelectedSource is { IsSupported: true } source &&
            source.SourceKind == AdvisorRecordVersion.EvidenceArtifactKind &&
            professionalAdvisoryRead is not null &&
            await professionalAdvisoryRead.CanReadSelectedSourceAsync(context.Request.TenantId,
                programId, handoffId, source, userId, ct).ConfigureAwait(false))
            return Result.Success;
        var membership = await memberships.GetAsync(context.Request.TenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.TenantId != context.Request.TenantId || membership.UserId != userId ||
            membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        return await independence.CanAuthorAsync(context.Request.TenantId, userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A person with actual Attest assignment history cannot author or approve this client's management records."));
    }
}
