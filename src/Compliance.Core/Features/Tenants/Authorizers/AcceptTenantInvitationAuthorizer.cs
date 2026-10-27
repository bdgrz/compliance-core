using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.Tenants.Authorizers;

sealed class AcceptTenantInvitationAuthorizer(IEmailAddressDirectoryReader emails)
    : IRequestAuthorizer<AcceptTenantInvitation>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<AcceptTenantInvitation> context, CancellationToken ct)
    {
        if (context.Request.InvitationId == Uuid.Empty || context.Request.TenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An invitation id and tenant are required."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Invitation acceptance requires a Bdgrz user identity."));
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));

        var address = await emails.GetAsync(normalized, ct).ConfigureAwait(false);
        return address?.UserId == userId && address.Verified
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Verify the invited email address before accepting."));
    }
}
