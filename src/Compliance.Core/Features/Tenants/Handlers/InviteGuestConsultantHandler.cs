using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class InviteGuestConsultantHandler(TenantInvitationAdmission admission,
    IProgramResourceScopeResolver programScopes) : IRequestHandler<InviteGuestConsultant>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<InviteGuestConsultant> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var invitedBy))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Guest invitations require a Bdgrz user identity."));
        if (request.TenantId == Uuid.Empty || request.ProgramIds is not { Count: > 0 } programIds ||
            programIds.Any(id => id == Uuid.Empty) ||
            programIds.Distinct().Count() != programIds.Count)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A guest invitation requires distinct program scopes in one organization."));

        foreach (var programId in programIds)
        {
            if (!await programScopes.IsTenantProgramAsync(request.TenantId, programId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "A guest invitation program was not found in this organization."));
        }

        return await admission.AdmitAsync(context, request.InvitationId, request.TenantId,
            nameof(InviteGuestConsultant), AuthoredIntentDigest.Compute(request), TenantInvitationTerms.ForGuest(
                request.EmailAddress, programIds, request.ExpiresOn, invitedBy,
                UserIdentityClaims.BdgrzDisplay(context.Actor, invitedBy)), ct).ConfigureAwait(false);
    }
}
