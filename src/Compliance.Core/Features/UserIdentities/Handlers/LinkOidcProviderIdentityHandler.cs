using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

public sealed class LinkOidcProviderIdentityHandler(IAggregateExecutor executor)
    : IRequestHandler<LinkOidcProviderIdentity, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<LinkOidcProviderIdentity> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) ||
            !OidcProviderClaims.TryGet(context.Actor, out var provider, out var identifier,
                out var emailAddress))
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(
                new RequestError(RequestErrorKind.Unauthorized,
                    "Linking an OIDC identity requires the current browser session and provider proof.")));

        return executor.ExecuteAsync(new UserIdentity(provider, identifier),
            identity => AggregateOutcome.CommitOnSuccess(identity.Register(userId, emailAddress)),
            context, ct);
    }
}
