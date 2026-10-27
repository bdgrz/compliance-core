using System.Text;
using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Processes;

/// <summary>One tenant route atomically binds the process key and root command id before any attempt runs.</summary>
sealed class FitzKvSystemDescriptionExportProcesses(IKvClient client) : ISystemDescriptionExportProcesses
{
    public async Task<SystemDescriptionExportProcessRecord?> ReadAsync(Uuid tenant, Uuid processId, Uuid commandId, CancellationToken ct)
    {
        await using var tx = await client.BeginAsync(Route(tenant), KvMode.ReadOnly, ct).ConfigureAwait(false);
        var command = await tx.GetAsync(CommandKey(commandId), ct).ConfigureAwait(false);
        if (command.Found)
            processId = Uuid.Parse(Encoding.UTF8.GetString(command.Value!.Value.Span), System.Globalization.CultureInfo.InvariantCulture);
        var row = await tx.GetAsync(Key(processId), ct).ConfigureAwait(false);
        if (!row.Found)
        {
            if (command.Found)
                throw new ImmutableContentIntegrityException("An export command index has no owning process.");
            return null;
        }
        var result = Parse(row.Value!.Value.Span);
        if (result.TenantId != tenant || result.ProcessId != processId)
            throw new ImmutableContentIntegrityException("The export process belongs to another tenant or process identity.");
        return result;
    }
    public async Task<Result<SystemDescriptionExportProcessRecord>> AcceptIfAbsentAsync(SystemDescriptionExportProcessRecord candidate, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await client.BeginAsync(Route(candidate.TenantId), KvMode.ReadWrite, ct).ConfigureAwait(false);
                var command = await tx.GetAsync(CommandKey(candidate.Binding.LogicalId), ct).ConfigureAwait(false);
                if (command.Found && Encoding.UTF8.GetString(command.Value!.Value.Span) != candidate.ProcessId.ToString())
                    return Conflict();
                var found = await tx.GetAsync(Key(candidate.ProcessId), ct).ConfigureAwait(false);
                if (found.Found)
                {
                    var existing = Parse(found.Value!.Value.Span);
                    return existing.Binding == candidate.Binding && existing.ProgramId == candidate.ProgramId && existing.DescriptionId == candidate.DescriptionId
                        ? Result<SystemDescriptionExportProcessRecord>.Success(existing) : Conflict();
                }
                if (command.Found)
                    throw new ImmutableContentIntegrityException("A bound export command lost its owning technical process.");
                var accepted = candidate with { Revision = 1 };
                await tx.InsertAsync(Key(candidate.ProcessId), JsonSerializer.SerializeToUtf8Bytes(accepted,
                    ComplianceCoreJsonContext.Default.SystemDescriptionExportProcessRecord), ct).ConfigureAwait(false);
                await tx.InsertAsync(CommandKey(candidate.Binding.LogicalId), Encoding.UTF8.GetBytes(candidate.ProcessId.ToString()), ct).ConfigureAwait(false);
                await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
                return Result<SystemDescriptionExportProcessRecord>.Success(accepted);
            }
            catch (KvException exception) when (attempt < 4 && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // Re-read the single-route authority after the competing admission.
            }
        }
    }
    public async Task<Result<SystemDescriptionExportProcessRecord>> ReplaceAsync(SystemDescriptionExportProcessRecord expected,
        SystemDescriptionExportProcessRecord replacement, CancellationToken ct)
    {
        if (replacement.Binding != expected.Binding || replacement.TenantId != expected.TenantId || replacement.ProgramId != expected.ProgramId ||
            replacement.DescriptionId != expected.DescriptionId || replacement.ProcessId != expected.ProcessId || replacement.Intent != expected.Intent)
            return Conflict();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await client.BeginAsync(Route(expected.TenantId), KvMode.ReadWrite, ct).ConfigureAwait(false);
                var found = await tx.GetAsync(Key(expected.ProcessId), ct).ConfigureAwait(false);
                if (!found.Found)
                    throw new ImmutableContentUnavailableException("The admitted export process is not readable yet.");
                var current = Parse(found.Value!.Value.Span);
                if (current.Binding != expected.Binding || current.Intent != expected.Intent)
                    return Conflict();
                if (current.State is "completed" or "failed")
                    return Result<SystemDescriptionExportProcessRecord>.Success(current);
                if (current.Revision != expected.Revision)
                    return Result<SystemDescriptionExportProcessRecord>.Failure(new RequestError(RequestErrorKind.Conflict, "The technical export attempt changed; retry the original operation.", true));
                var replaced = replacement with { Revision = current.Revision + 1 };
                await tx.PutAsync(Key(expected.ProcessId), JsonSerializer.SerializeToUtf8Bytes(replaced,
                    ComplianceCoreJsonContext.Default.SystemDescriptionExportProcessRecord), ct).ConfigureAwait(false);
                await tx.CommitAsync(KvDurability.Sync, ct).ConfigureAwait(false);
                return Result<SystemDescriptionExportProcessRecord>.Success(replaced);
            }
            catch (KvException exception) when (attempt < 4 && FitzKvProcessRecordStore.IsIsolationConflict(exception))
            {
                // Unknown or concurrent attempts recover from their persisted forward state.
            }
        }
    }
    static string Route(Uuid tenant) => $"kv://bdgrz/system-description-export-processes/{tenant}";
    static byte[] Key(Uuid id) => Encoding.UTF8.GetBytes("e/" + id);
    static byte[] CommandKey(Uuid id) => Encoding.UTF8.GetBytes("c/" + id);
    static SystemDescriptionExportProcessRecord Parse(ReadOnlySpan<byte> bytes)
    {
        SystemDescriptionExportProcessRecord record;
        try
        {
            record = JsonSerializer.Deserialize(bytes, ComplianceCoreJsonContext.Default.SystemDescriptionExportProcessRecord)
                ?? throw new ImmutableContentIntegrityException("An export process cannot be empty.");
        }
        catch (JsonException)
        {
            throw new ImmutableContentIntegrityException("The export process contains malformed persisted technical state.");
        }
        if (record.TenantId == Uuid.Empty || record.ProgramId == Uuid.Empty || record.DescriptionId == Uuid.Empty || record.ProcessId == Uuid.Empty ||
            record.Binding is null || record.Intent is null || record.Intent.Schema != SystemDescriptionExportIntent.Schema ||
            record.Binding.Realm != record.TenantId.ToString() || record.Revision < 1 || record.Attempts < 0 ||
            record.State is not ("accepted" or "running" or "completed" or "failed") ||
            record.State == "completed" && (record.Output is null || record.OperationResult is null) ||
            record.State == "failed" && string.IsNullOrWhiteSpace(record.FailureCode))
            throw new ImmutableContentIntegrityException("The export process has another exact identity, state or immutable input.");
        return record;
    }
    static Result<SystemDescriptionExportProcessRecord> Conflict() => Result<SystemDescriptionExportProcessRecord>.Failure(new RequestError(RequestErrorKind.Conflict,
        "The export identity is already bound to another exact owner, actor or authored intent."));
}
