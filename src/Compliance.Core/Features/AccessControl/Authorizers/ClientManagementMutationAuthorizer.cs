using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

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
