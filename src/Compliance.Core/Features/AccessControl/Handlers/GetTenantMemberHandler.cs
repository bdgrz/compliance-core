using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetTenantMemberHandler(ITenantMembershipDirectoryReader memberships,
    IEmailAddressDirectoryReader? emails = null, IUserDisplayNameReader? profiles = null,
    IPersonMemberDisplayReader? people = null)
    : IRequestHandler<GetTenantMember, TenantMembershipView>
{
    public async ValueTask<Result<TenantMembershipView>> HandleAsync(
        IRequestContext<GetTenantMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var membership = await memberships.GetAsync(request.TenantId.ToString(), request.UserId, ct)
            .ConfigureAwait(false);
        return membership is null || membership.TenantId != request.TenantId
            ? Result<TenantMembershipView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."))
            : Result<TenantMembershipView>.Success(
                await MemberPresentationEnrichment.WithMemberAsync(emails, membership, ct,
                    profiles, people).ConfigureAwait(false));
    }
}
