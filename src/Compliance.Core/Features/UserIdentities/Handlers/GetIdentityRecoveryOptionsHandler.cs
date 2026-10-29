using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

public sealed class GetIdentityRecoveryOptionsHandler(
    IAggregateReader aggregates,
    IUserIdentityDirectoryReader identities,
    IUserIdentityDirectoryReadConsistency consistency,
    TimeProvider clock) : IRequestHandler<GetIdentityRecoveryOptions, IdentityRecoveryOptions>
{
    public async ValueTask<Result<IdentityRecoveryOptions>> HandleAsync(
        IRequestContext<GetIdentityRecoveryOptions> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result<IdentityRecoveryOptions>.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var address = await aggregates.HydrateAsync(new EmailAddress(normalized, Uuid.Empty, null, context.Request.ChallengeId), ct).ConfigureAwait(false);
        if (address.Owner is not { } owner)
            return Result<IdentityRecoveryOptions>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "No verified recovery address is available."));
        var proof = address.ValidateIdentityRecoveryChallenge(owner, context.Request.ChallengeId,
            context.Request.Token, clock.GetUtcNow());
        if (!proof.IsSuccess)
            return Result<IdentityRecoveryOptions>.Failure(proof.Error);

        var caughtUp = await consistency.EnsureCaughtUpAsync(ct).ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            return Result<IdentityRecoveryOptions>.Failure(caughtUp.Error);
        if (context.Request.Limit is < 1 or > 100)
            return Result<IdentityRecoveryOptions>.Failure(new RequestError(RequestErrorKind.Validation,
                "Identity recovery options limit must be between 1 and 100."));
        if (context.Request.MinimumProjectionRevision is < 0)
            return Result<IdentityRecoveryOptions>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum identity directory revision cannot be negative."));

        var snapshot = await identities.ListAsync(owner, context.Request.Limit ?? 50,
            context.Request.Cursor, ct).ConfigureAwait(false);
        if (context.Request.MinimumProjectionRevision is { } minimum && snapshot.Revision < minimum)
            return Result<IdentityRecoveryOptions>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity directory has not reached the requested revision. Retry the query.",
                isTransient: true));
        var options = new Page<IdentityRecoveryOption>(snapshot.Identities.Items.Select(identity =>
                new IdentityRecoveryOption(identity.UserIdentityId, identity.Provider)).ToArray(),
            snapshot.Identities.NextCursor);
        return Result<IdentityRecoveryOptions>.Success(
            new IdentityRecoveryOptions(snapshot.Revision, options));
    }
}
