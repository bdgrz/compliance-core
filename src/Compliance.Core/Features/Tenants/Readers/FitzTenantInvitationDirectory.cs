using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Readers;

sealed class FitzTenantInvitationDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/tenant-invitation-directory/projection",
        "TenantInvitationDirectory"),
      ITenantInvitationDirectoryReader, ITenantInvitationDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TenantMemberInvited invited:
                var next = new TenantInvitationDirectoryEntry(invited.TenantId, invited.InvitationId,
                    invited.EmailAddress, invited.Affiliation, invited.Administrator, invited.BuiltInRole,
                    invited.ExpiresAt, invited.InvitedBy, null, invited.GuestProgramIdsManifest, invited.GuestExpiresOn);
                var prior = await TenantInvitationDirectorySchema.Directory.GetAsync(Transaction,
                    invited.EmailAddress, ct).ConfigureAwait(false);
                if (prior is null)
                    await TenantInvitationDirectorySchema.Directory.InsertAsync(Transaction,
                        next, ct).ConfigureAwait(false);
                else
                    await TenantInvitationDirectorySchema.Directory.ReplaceAsync(Transaction,
                        prior, next, ct).ConfigureAwait(false);
                break;
            case TenantInvitationAccepted accepted:
                var current = await TenantInvitationDirectorySchema.Directory.GetAsync(Transaction,
                    accepted.EmailAddress, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != accepted.TenantId)
                    throw new InvalidOperationException(
                        "An invitation cannot be accepted before it is projected.");
                // A later invitation of the same mailbox already replaced this one; its acceptance is history.
                if (current.InvitationId == accepted.InvitationId)
                    await TenantInvitationDirectorySchema.Directory.ReplaceAsync(Transaction,
                        current, current with { AcceptedUserId = accepted.UserId }, ct)
                        .ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId,
        string emailAddress, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TenantInvitationDirectorySchema.Directory.GetAsync(tx, emailAddress, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TenantInvitationDirectorySchema.Directory.QueryAsync(tx,
            TenantInvitationDirectorySchema.ByEmail.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
