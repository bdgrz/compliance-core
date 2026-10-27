using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

sealed class FitzPersonDirectory(IKvClient client, PersonReader? people = null)
    : FitzKvProjectionStore(client, "kv://bdgrz/person-directory-v2/projection",
            "PersonDirectoryV2"),
        IPersonDirectoryReader, IPersonDirectoryProjection, IPersonMemberDisplayReader
{
    public const string ManualSource = "manual";

    public async ValueTask<string?> ReadAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            return null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var matches = await PersonDirectorySchema.People.QueryAsync(tx,
            PersonDirectorySchema.ByCorrelatedUser.Query().WithPrefix(userId.ToString()).Take(2), ct)
            .ConfigureAwait(false);
        return matches.Items.Count == 1 && matches.Items[0].TenantId == tenantId &&
               matches.Items[0].CorrelatedUserId == userId
            ? matches.Items[0].DisplayName
            : null;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("PersonDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString(), "people")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case IPersonNativeFact native:
                if (people is null)
                    throw new InvalidOperationException("Native person projection requires its verified historical source reader.");
                var actual = await people.ReadFactAsync(domainEvent, ct).ConfigureAwait(false);
                // The directory is bounded discovery. Authorized detail reads load private values from the verified owner body.
                var verified = actual with { PersonalContact = null, RestrictedFieldsRedacted = true };
                var prior = await PersonDirectorySchema.People.GetAsync(Transaction, verified.PersonId, ct).ConfigureAwait(false);
                if (prior is null)
                {
                    if (domainEvent is not PersonRegistrationRecorded || verified.Revision != 1)
                        throw new InvalidOperationException("A native person revision cannot project before its creation.");
                    await PersonDirectorySchema.People.InsertAsync(Transaction, verified, ct).ConfigureAwait(false);
                }
                else
                {
                    if (prior.TenantId != verified.TenantId || verified.Revision != prior.Revision + (native.Changed ? 1 : 0) ||
                        !native.Changed && (prior with { PersonalContact = null, RestrictedFieldsRedacted = true }) != verified)
                        throw new InvalidOperationException("A native person projection requires its exact preceding revision.");
                    if (prior != verified)
                        await PersonDirectorySchema.People.ReplaceAsync(Transaction, prior, verified, ct).ConfigureAwait(false);
                }
                break;
            case PersonRecorded recorded:
                await PersonDirectorySchema.People.InsertAsync(Transaction,
                    new PersonView(recorded.TenantId, recorded.PersonId, 1,
                        recorded.DisplayName, recorded.WorkEmail, ManualSource,
                        recorded.Actor, recorded.ChangedAt, PersonalContact: recorded.PersonalContact), ct).ConfigureAwait(false);
                break;
            case PersonRevised revised:
                var current = await PersonDirectorySchema.People.GetAsync(Transaction,
                    revised.PersonId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A person revision cannot project before its predecessor.");
                await PersonDirectorySchema.People.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        DisplayName = revised.DisplayName,
                        WorkEmail = revised.WorkEmail,
                        PersonalContact = revised.PersonalContact,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
            case PersonMembershipCorrelated correlated:
                var linked = await PersonDirectorySchema.People.GetAsync(Transaction,
                    correlated.PersonId, ct).ConfigureAwait(false);
                if (linked is null || linked.TenantId != correlated.TenantId ||
                    linked.Revision + 1 != correlated.Revision)
                    throw new InvalidOperationException(
                        "A person correlation cannot project before its predecessor.");
                await PersonDirectorySchema.People.ReplaceAsync(Transaction, linked,
                    linked with
                    {
                        Revision = correlated.Revision,
                        CorrelatedUserId = correlated.UserId,
                        LastChangedBy = correlated.Actor,
                        LastChangedAt = correlated.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<PersonView?> GetAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await PersonDirectorySchema.People.GetAsync(tx, personId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<PersonView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await PersonDirectorySchema.People.QueryAsync(tx,
            PersonDirectorySchema.ByDisplayName.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
