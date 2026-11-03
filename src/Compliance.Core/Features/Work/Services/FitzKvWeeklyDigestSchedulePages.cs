using System.Text;
using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Models;

namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Short tenant-local transactions bind a page once; bulk content stays in verified immutable storage.</summary>
sealed class FitzKvWeeklyDigestSchedulePages(IKvClient client) : IWeeklyDigestSchedulePages
{
    public async Task<WeeklyDigestSchedulePage?> ReadAsync(Uuid tenantId, Uuid pageId, CancellationToken ct)
    {
        await using var tx = await client.BeginAsync(Route(tenantId), KvMode.ReadOnly, ct).ConfigureAwait(false);
        var found = await tx.GetAsync(Key(pageId), ct).ConfigureAwait(false);
        return found.Found ? Parse(found.Value!.Value.Span, tenantId, pageId) : null;
    }

    public async Task<Result<WeeklyDigestSchedulePage>> AcceptIfAbsentAsync(WeeklyDigestSchedulePage candidate,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await client.BeginAsync(Route(candidate.TenantId), KvMode.ReadWrite, ct)
                    .ConfigureAwait(false);
                var found = await tx.GetAsync(Key(candidate.PageId), ct).ConfigureAwait(false);
                if (found.Found)
                {
                    var existing = Parse(found.Value!.Value.Span, candidate.TenantId, candidate.PageId);
                    return existing.Limit == candidate.Limit
                        ? Result<WeeklyDigestSchedulePage>.Success(existing)
                        : Result<WeeklyDigestSchedulePage>.Failure(ChangedIntent());
                }
                await tx.InsertAsync(Key(candidate.PageId), JsonSerializer.SerializeToUtf8Bytes(candidate,
                    ComplianceCoreJsonContext.Default.WeeklyDigestSchedulePage), ct).ConfigureAwait(false);
                await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
                return Result<WeeklyDigestSchedulePage>.Success(candidate);
            }
            catch (KvException exception) when (attempt < 4 && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // The competing admission's immutable selection wins.
            }
        }
    }

    public async Task<WeeklyDigestSchedulePage> CompleteAsync(WeeklyDigestSchedulePage admitted,
        ImmutableContentRef result, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await client.BeginAsync(Route(admitted.TenantId), KvMode.ReadWrite, ct)
                    .ConfigureAwait(false);
                var found = await tx.GetAsync(Key(admitted.PageId), ct).ConfigureAwait(false);
                if (!found.Found)
                    throw new ImmutableContentUnavailableException("The admitted schedule page is not readable yet.");
                var current = Parse(found.Value!.Value.Span, admitted.TenantId, admitted.PageId);
                if (current.Limit != admitted.Limit || current.Plan != admitted.Plan)
                    throw new ImmutableContentIntegrityException("The schedule page's admitted intent changed.");
                if (current.Result is not null)
                    return current;
                var completed = current with { Result = result };
                await tx.PutAsync(Key(admitted.PageId), JsonSerializer.SerializeToUtf8Bytes(completed,
                    ComplianceCoreJsonContext.Default.WeeklyDigestSchedulePage), ct).ConfigureAwait(false);
                await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
                return completed;
            }
            catch (KvException exception) when (attempt < 4 && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // Equal callers converge on the first durable original response.
            }
        }
    }

    static WeeklyDigestSchedulePage Parse(ReadOnlySpan<byte> bytes, Uuid tenantId, Uuid pageId)
    {
        WeeklyDigestSchedulePage row;
        try
        {
            row = JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.WeeklyDigestSchedulePage)
                ?? throw new ImmutableContentIntegrityException("A schedule page cannot be empty.");
        }
        catch (JsonException)
        {
            throw new ImmutableContentIntegrityException("A schedule page contains malformed technical state.");
        }
        if (row.TenantId != tenantId || row.PageId != pageId || row.Limit is < 1 or > 200 ||
            row.Plan is null || row.Plan.Schema != WeeklyDigestSchedulePage.PlanSchema ||
            row.Result is { } result && result.Schema != WeeklyDigestSchedulePage.ResultSchema)
            throw new ImmutableContentIntegrityException("A schedule page has another identity or content schema.");
        return row;
    }

    internal static RequestError ChangedIntent() => new(RequestErrorKind.Conflict,
        "The schedule page identity already belongs to a different page limit.");
    static string Route(Uuid tenantId) => $"kv://bdgrz/weekly-digest-schedule-pages/{tenantId}";
    static byte[] Key(Uuid pageId) => Encoding.UTF8.GetBytes("p/" + pageId);
}
