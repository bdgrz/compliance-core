using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class TenantInvitationIssuer(IAggregateExecutor executor,
    EmailChallengeTokenKeys tokenKeys, TimeProvider clock)
{
    public async ValueTask<Result> IssueAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, string emailAddress, string affiliation, bool administrator,
        string? builtInRole, Uuid invitedBy, CancellationToken ct)
        where TRequest : IRequestBase
    {
        return await IssueCoreAsync(context, tenantId, emailAddress, invitedBy,
            (invitation, hash, expiresAt, now, attemptId, keyId) => invitation.Invite(affiliation,
                administrator, hash, expiresAt, now, invitedBy, builtInRole, attemptId, keyId), ct)
            .ConfigureAwait(false);
    }

    public ValueTask<Result> IssueGuestAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, string emailAddress, IReadOnlyList<Uuid> programIds, DateOnly expiresOn,
        Uuid invitedBy, string invitedByDisplay, CancellationToken ct) where TRequest : IRequestBase
    {
        var scopes = programIds?.ToArray() ?? [];
        return IssueCoreAsync(context, tenantId, emailAddress, invitedBy,
            (invitation, hash, expiresAt, now, attemptId, keyId) => invitation.InviteGuest(scopes,
                expiresOn, hash, expiresAt, now, invitedBy, attemptId, keyId, invitedByDisplay), ct);
    }

    async ValueTask<Result> IssueCoreAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, string emailAddress, Uuid invitedBy,
        Func<TenantInvitation, string, DateTimeOffset, DateTimeOffset, Uuid, string, Result> issue,
        CancellationToken ct) where TRequest : IRequestBase
    {
        if (!EmailAddresses.TryNormalize(emailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var attemptId = Uuid.CreateVersion4();
        var now = clock.GetUtcNow();
        var expiresAt = now.AddDays(7);
        var keyId = tokenKeys.ActiveKeyId;
        var token = tokenKeys.DeriveInvitation(keyId, attemptId, tenantId, normalized, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        // A delivery outcome can commit while an administrator reissues the
        // invitation. Keep this attempt's token fixed and re-evaluate its purpose
        // against fresh state after a stream conflict.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(new TenantInvitation(tenantId, normalized),
                    invitation => AggregateOutcome.CommitOnSuccess(issue(invitation, hash,
                        expiresAt, now, attemptId, keyId)), context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
