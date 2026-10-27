using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>
///     Returns one work relationship, disclosing the restricted manager chain only to an actor with
///     the <c>workforce.manager_chain</c> field grant (M0-D06).
/// </summary>
public sealed class GetWorkRelationshipHandler(WorkRelationshipReadConsistency consistency,
    IPermissionAuthorizer permissions) : IRequestHandler<GetWorkRelationship, WorkRelationshipView>
{
    public async ValueTask<Result<WorkRelationshipView>> HandleAsync(
        IRequestContext<GetWorkRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        var view = await consistency.GetAsync(request.TenantId, request.RelationshipId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!view.IsSuccess)
            return view;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        var personalDetails = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
        return Result<WorkRelationshipView>.Success(view.Value with
        {
            ManagerPersonId = managerChain.CanRead ? view.Value.ManagerPersonId : null,
            EmploymentStatusReason = personalDetails.CanRead
                ? view.Value.EmploymentStatusReason
                : null,
            RestrictedFieldsRedacted = view.Value.RestrictedFieldsRedacted ||
                !managerChain.CanRead || !personalDetails.CanRead,
        });
    }
}
