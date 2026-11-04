using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.Evidence.Services;

/// <summary>Authoritative, bounded status reads with independent current artifact discovery checks.</summary>
sealed class EvidenceSupportStatusRead(IAggregateReader reader, IDomainEventReader events,
    EvidenceArtifactReadAccess access)
{
    const int MaximumSourceRecords = 20000;

    public async ValueTask<Result<EvidenceSupportStatusView>> GetAsync(IRequestContext context, Uuid tenantId,
        Uuid programId, Uuid supportId, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(context, tenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceSupportStatusView>.Failure(allowed.Error!);
        if (programId == Uuid.Empty || supportId == Uuid.Empty)
            return Missing();
        var owner = await reader.HydrateAsync(EvidenceSupport.ForVerification(tenantId, supportId), ct).ConfigureAwait(false);
        if (owner.Current?.ProgramId != programId)
            return Missing();
        allowed = await access.RequireAsync(context, tenantId, owner.Current.ArtifactId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceSupportStatusView>.Failure(allowed.Error!);
        var value = EvidenceSupportReader.Status(owner);
        allowed = await access.RequireAsync(context, tenantId, owner.Current.ArtifactId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceSupportStatusView>.Failure(allowed.Error!);
        var confirmed = await reader.HydrateAsync(EvidenceSupport.ForVerification(tenantId, supportId), ct).ConfigureAwait(false);
        return confirmed.CommittedStreamPosition == owner.CommittedStreamPosition
            ? Result<EvidenceSupportStatusView>.Success(value)
            : Result<EvidenceSupportStatusView>.Failure(Changed());
    }

    public async ValueTask<Result<Page<EvidenceSupportStatusView>>> ListAsync(IRequestContext context,
        ListEvidenceSupports request, CancellationToken ct)
    {
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.Limit is < 1 or > 200 ||
            request.Status is not (null or "active" or "withdrawn"))
            return Fail(new RequestError(RequestErrorKind.Validation, "Support status must be active or withdrawn and limit must be between 1 and 200."));
        var limit = request.Limit ?? 50;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Fail(new RequestError(RequestErrorKind.Unauthorized, "Support status requires a personal Bdgrz identity."));
        if (!TryCursor(request, userId, limit, out var previousCut, out var offset))
            return Fail(new RequestError(RequestErrorKind.Validation, "The support cursor is invalid for this scope, filter, actor or page size."));
        var allowed = await access.RequireMembershipAsync(context, request.TenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Fail(allowed.Error!);
        var source = await CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Fail(source.Error!);
        var visible = new List<EvidenceSupportStatusView>();
        foreach (var identity in source.Value.Identities)
        {
            var owner = await reader.HydrateAsync(EvidenceSupport.ForVerification(request.TenantId, identity.Id), ct).ConfigureAwait(false);
            if (owner.Current is null || owner.CommittedStreamPosition != identity.Position ||
                owner.Current.Metadata.EventId != identity.LinkEventId ||
                owner.Withdrawal?.Metadata.EventId != identity.WithdrawalEventId)
                return Fail(Changed());
            if (owner.Current.ProgramId != request.ProgramId)
                continue;
            allowed = await access.RequireAsync(context, request.TenantId, owner.Current.ArtifactId, ct).ConfigureAwait(false);
            if (!allowed.IsSuccess)
            {
                if (allowed.Error?.Kind == RequestErrorKind.NotFound)
                    continue;
                return Fail(allowed.Error!);
            }
            var status = EvidenceSupportReader.Status(owner);
            if (request.Status is null || status.IsActive == (request.Status == "active"))
                visible.Add(status);
        }
        var ordered = visible.OrderBy(static value => value.Original.LinkedAt)
            .ThenBy(static value => value.Original.EvidenceSupportId.ToString(), StringComparer.Ordinal).ToArray();
        var cut = Digest(source.Value.Fingerprint + "\n" + string.Join('\n', ordered.Select(static value =>
            value.Original.EvidenceSupportId.ToString() + ":" + value.Revision.ToString(CultureInfo.InvariantCulture))));
        if (previousCut is not null && previousCut != cut)
            return Fail(Changed());
        if (offset > ordered.Length)
            return Fail(new RequestError(RequestErrorKind.Validation, "The support cursor is outside its exact source."));
        var values = ordered.Skip(offset).Take(limit).ToArray();
        // Permission loss during assembly must not publish a previously visible artifact or withdrawal rationale.
        foreach (var value in values)
        {
            allowed = await access.RequireAsync(context, request.TenantId, value.Original.ArtifactId, ct).ConfigureAwait(false);
            if (!allowed.IsSuccess)
                return Fail(allowed.Error!);
        }
        allowed = await access.RequireMembershipAsync(context, request.TenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Fail(allowed.Error!);
        var confirmed = await CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Fail(confirmed.Error!);
        if (confirmed.Value.Fingerprint != source.Value.Fingerprint)
            return Fail(Changed());
        var next = offset + values.Length < ordered.Length
            ? Wrap(request, userId, limit, cut, offset + values.Length) : null;
        return Result<Page<EvidenceSupportStatusView>>.Success(new(values, next));
    }

    async ValueTask<Result<Source>> CaptureAsync(Uuid tenantId, CancellationToken ct)
    {
        var identities = new Dictionary<Uuid, Identity>();
        var records = new List<string>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           "evidence-support"), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (records.Count >= MaximumSourceRecords)
                return Result<Source>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The bounded support status source scan is incomplete."));
            var id = record.Event.Metadata.AggregateId;
            if (id == Uuid.Empty || record.Event.Metadata.EventId == Uuid.Empty ||
                record.Stream != EvidenceSupport.ForVerification(tenantId, id).Stream)
                return Result<Source>.Failure(InvalidHistory());
            switch (record.Event)
            {
                case EvidenceSupportLinked linked when linked.TenantId == tenantId && linked.SubjectId == id &&
                    linked.Revision == 1 && record.ResourceOffset == 0 && !identities.ContainsKey(id):
                    identities.Add(id, new(id, 1, linked.Metadata.EventId, null));
                    break;
                case EvidenceSupportWithdrawn withdrawn when withdrawn.TenantId == tenantId && withdrawn.SubjectId == id &&
                    withdrawn.Revision == 2 && record.ResourceOffset == 1 &&
                    identities.TryGetValue(id, out var identity) && identity.Position == 1:
                    identities[id] = identity with { Position = 2, WithdrawalEventId = withdrawn.Metadata.EventId };
                    break;
                default:
                    return Result<Source>.Failure(InvalidHistory());
            }
            records.Add(id.ToString() + ":" + record.ResourceOffset.ToString(CultureInfo.InvariantCulture) + ":" +
                record.Event.Metadata.EventId.ToString());
        }
        return Result<Source>.Success(new(Digest(string.Join('\n', records.Order(StringComparer.Ordinal))),
            identities.Values.ToArray()));
    }

    static bool TryCursor(ListEvidenceSupports request, Uuid userId, int limit, out string? cut, out int offset)
    {
        cut = null;
        offset = 0;
        if (request.Cursor is null)
            return true;
        if (request.Cursor.Length > 2048)
            return false;
        try
        {
            var values = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(request.Cursor)).Split('\n');
            if (values.Length != 8 || values[0] != "v1" || values[1] != request.TenantId.ToString() ||
                values[2] != request.ProgramId.ToString() || values[3] != userId.ToString() ||
                values[4] != (request.Status ?? "all") || values[5] != limit.ToString(CultureInfo.InvariantCulture) ||
                values[6].Length != 64 || values[6].Any(static ch => ch is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
                !int.TryParse(values[7], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 1)
                return false;
            cut = values[6];
            return true;
        }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
    }

    static string Wrap(ListEvidenceSupports request, Uuid userId, int limit, string cut, int offset) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('\n', "v1", request.TenantId.ToString(),
            request.ProgramId.ToString(), userId.ToString(), request.Status ?? "all", limit.ToString(CultureInfo.InvariantCulture),
            cut, offset.ToString(CultureInfo.InvariantCulture))));

    static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static RequestError Changed() => new(RequestErrorKind.Conflict, "The support status source changed. Restart the query.", true);
    static RequestError InvalidHistory() => new(RequestErrorKind.Conflict, "The support status source history cannot be verified.");
    static Result<EvidenceSupportStatusView> Missing() => Result<EvidenceSupportStatusView>.Failure(
        new RequestError(RequestErrorKind.NotFound, "The evidence support link was not found in this program."));
    static Result<Page<EvidenceSupportStatusView>> Fail(RequestError error) => Result<Page<EvidenceSupportStatusView>>.Failure(error);

    sealed record Identity(Uuid Id, ulong Position, Uuid LinkEventId, Uuid? WithdrawalEventId);
    sealed record Source(string Fingerprint, IReadOnlyList<Identity> Identities);
}
