using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Workforce.Contracts;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

sealed class CassiePersonDirectory(CassieProjectionDatabase database, PersonReader? people = null)
    : CassieWorkforceRepository(database, "PersonDirectoryV2", "people"),
        IPersonDirectoryReader, IPersonDirectoryProjection, IPersonMemberDisplayReader
{
    public const string ManualSource = "manual";

    public async ValueTask<string?> ReadAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            return null;
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT row_json FROM workforce_people WHERE projection_key = @scope AND correlated_user_id = @user ORDER BY model_id LIMIT 2");
        Bind(command, ("scope", LiveProjectionKey(tenantId)), ("user", userId.ToString()));
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rows.ReadAsync(ct).ConfigureAwait(false))
            return null;
        var match = Deserialize(rows.GetString(0), ComplianceCoreJsonContext.Default.PersonView);
        return !await rows.ReadAsync(ct).ConfigureAwait(false) && match.TenantId == tenantId && match.CorrelatedUserId == userId
            ? match.DisplayName : null;
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
                var prior = await FindPersonAsync(verified.PersonId, ct).ConfigureAwait(false);
                if (prior is null)
                {
                    if (domainEvent is not PersonRegistrationRecorded || verified.Revision != 1)
                        throw new InvalidOperationException("A native person revision cannot project before its creation.");
                    await InsertPersonAsync(verified, ct).ConfigureAwait(false);
                }
                else
                {
                    if (prior.TenantId != verified.TenantId || verified.Revision != prior.Revision + (native.Changed ? 1 : 0) ||
                        !native.Changed && (prior with { PersonalContact = null, RestrictedFieldsRedacted = true }) != verified)
                        throw new InvalidOperationException("A native person projection requires its exact preceding revision.");
                    if (prior != verified)
                        await ReplacePersonAsync(verified, ct).ConfigureAwait(false);
                }
                break;
            case PersonRecorded recorded:
                await InsertPersonAsync(new PersonView(recorded.TenantId, recorded.PersonId, 1,
                        recorded.DisplayName, recorded.WorkEmail, ManualSource,
                        recorded.Actor, recorded.ChangedAt, PersonalContact: recorded.PersonalContact), ct).ConfigureAwait(false);
                break;
            case PersonRevised revised:
                var current = await FindPersonAsync(revised.PersonId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A person revision cannot project before its predecessor.");
                await ReplacePersonAsync(current with
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
                var linked = await FindPersonAsync(correlated.PersonId, ct).ConfigureAwait(false);
                if (linked is null || linked.TenantId != correlated.TenantId ||
                    linked.Revision + 1 != correlated.Revision)
                    throw new InvalidOperationException(
                        "A person correlation cannot project before its predecessor.");
                await ReplacePersonAsync(linked with
                    {
                        Revision = correlated.Revision,
                        CorrelatedUserId = correlated.UserId,
                        LastChangedBy = correlated.Actor,
                        LastChangedAt = correlated.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<PersonView?> GetAsync(Uuid tenantId, Uuid personId, CancellationToken ct = default)
    {
        var view = await ReadRowAsync("workforce_people", tenantId, personId,
            ComplianceCoreJsonContext.Default.PersonView, ct).ConfigureAwait(false);
        return view?.TenantId == tenantId ? view : null;
    }

    public ValueTask<Page<PersonView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        ReadPageAsync("workforce_people", tenantId, ComplianceCoreJsonContext.Default.PersonView, limit, cursor, "all", ct);

    ValueTask<PersonView?> FindPersonAsync(Uuid id, CancellationToken ct) =>
        FindCurrentAsync("workforce_people", id, ComplianceCoreJsonContext.Default.PersonView, ct);

    ValueTask InsertPersonAsync(PersonView row, CancellationToken ct) =>
        InsertRowAsync("workforce_people", row.TenantId, row.PersonId, row,
            ComplianceCoreJsonContext.Default.PersonView, ct, ("sort_key", row.DisplayName.ToUpperInvariant()), ("correlated_user_id", row.CorrelatedUserId?.ToString() ?? string.Empty));

    ValueTask ReplacePersonAsync(PersonView row, CancellationToken ct) =>
        ReplaceRowAsync("workforce_people", row.TenantId, row.PersonId, row,
            ComplianceCoreJsonContext.Default.PersonView, ct, ("sort_key", row.DisplayName.ToUpperInvariant()), ("correlated_user_id", row.CorrelatedUserId?.ToString() ?? string.Empty));
}
