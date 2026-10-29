using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Programs.Authorizers;
namespace Bdgrz.Compliance.Features.Policies.Rules;

/// <summary>The acting member resolved from an authorized request.</summary>
public sealed record PolicyActor(Uuid UserId, Uuid MemberId, ActorReference Reference)
{
    public static PolicyActor From<TRequest>(IRequestContext<TRequest> context, Uuid tenantId)
        where TRequest : IRequestBase
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(tenantId, userId);
        return new PolicyActor(userId, memberId, ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId)));
    }
}
