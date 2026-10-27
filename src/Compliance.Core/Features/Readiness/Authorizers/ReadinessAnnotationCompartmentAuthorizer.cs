using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Readiness.Authorizers;

/// <summary>Protects existing advisor feedback while leaving shared management readiness reads under ordinary grants.</summary>
sealed class ReadinessAnnotationCompartmentAuthorizer(ITenantMembershipDirectoryReader memberships,
    ClientCompartmentIndependenceGuard independence, ProfessionalAdvisoryReadAccess professionalAccess)
    : IRequestAuthorizer<ListReadinessAnnotations>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ListReadinessAnnotations> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Advisory feedback requires an attributable Bdgrz user."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        var clientMember = membership is not null && membership.TenantId == tenantId &&
            membership.UserId == userId && !membership.IsSuspended && !membership.IsDeprovisioned &&
            membership.Affiliation != "firm_staff";
        var assignedProfessional = !clientMember && await professionalAccess.CanReadAsync(tenantId,
            context.Request.ProgramId, userId, ct).ConfigureAwait(false);
        if (!clientMember && !assignedProfessional)
            return Result.Failure(new RequestError(membership is null || membership.TenantId != tenantId ||
                membership.UserId != userId || membership.IsSuspended || membership.IsDeprovisioned
                    ? RequestErrorKind.NotFound : RequestErrorKind.Forbidden,
                "The tenant was not found."));
        return await independence.CanReadAsync(tenantId, userId, RecordCompartment.AdvisoryWorkingNotes, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Actual Attest assignment history prevents reading this client's advisory working notes."));
    }
}
