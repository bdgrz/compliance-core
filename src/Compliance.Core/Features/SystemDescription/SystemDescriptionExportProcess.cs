using Cntryl.Fitz;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Resumable technical export over one frozen approved source. It never mutates description revision or issues delivery tickets.</summary>
public sealed class SystemDescriptionExportProcess(ISystemDescriptionExportProcesses processes, SystemDescriptionReader descriptions,
    IImmutableContentStore content, IArtifactContentStore artifacts, TimeProvider clock)
{
    public const string Name = "SystemDescriptionExportProcess";
    public const string Area = "system-description-exports";
    public static Uuid IdFor(Uuid description, long version, string key) => Uuid.CreateVersion5(description, FormattableString.Invariant($"export:{version}:{key.Trim()}"));
    public async ValueTask<Result> ExecuteAsync(IRequestContext<GenerateSystemDescriptionExport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!SystemDescriptionCommandActor.TryGet(context.Actor, request.TenantId, out var member, out var display) || RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A technical description export requires an authorized Bdgrz member actor.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.DescriptionId == Uuid.Empty ||
            request.Version < 1 || display.Length > 200 || request.Format is not ("pdf" or "docx") || string.IsNullOrWhiteSpace(request.ExportKey) || request.ExportKey.Trim().Length > 400)
            return Failure(RequestErrorKind.Validation, "An export requires exact identities, an approved version, a supported format and a stable export key.");
        var id = IdFor(request.DescriptionId, request.Version, request.ExportKey);
        var stream = new EventStreamAddress(request.TenantId.ToString(), Area, id.ToString());
        var operation = new OperationProvenance(request.CommandId, request.CommandId, null, nameof(GenerateSystemDescriptionExport),
            ActorReference.ForMember(member, display), stream, id.ToString(), null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow(), stream, stream, stream);
        var binding = AcceptedOperationBinding.Of(operation);
        SystemDescriptionExportProcessRecord? record = null;
        try
        {
            record = await processes.ReadAsync(request.TenantId, id, request.CommandId, ct).ConfigureAwait(false);
            if (record is not null && !Matches(record, binding, request.ProgramId, request.DescriptionId, id))
                return Failure(RequestErrorKind.Conflict, "The export identity is already bound to another exact owner, actor or authored intent.");
            if (record is null)
            {
                var source = await descriptions.VersionSourceAsync(request.TenantId, request.ProgramId, request.DescriptionId, request.Version, ct).ConfigureAwait(false);
                if (source is not { Version.Status: "approved", ApprovalSource: not null, Approval: not null })
                    return await ResolveFailureAsync(request, binding, id, new RequestError(RequestErrorKind.Conflict,
                        "Only an exact independently approved description version can be exported."), ct).ConfigureAwait(false);
                var value = new SystemDescriptionExportIntent(request.TenantId, request.ProgramId, request.DescriptionId, id,
                    source.Version.SystemDescriptionVersionId, source.Version.Version, request.Format, request.ExportKey.Trim(), source.Version.ContentSha256,
                    source.Content, source.Source, source.ApprovalSource, operation);
                // Candidate ids are fresh: concurrent equal requests may freeze different server timestamps before one wins admission.
                var intent = await content.WriteJsonAsync(request.TenantId.ToString(), Uuid.CreateVersion4(), SystemDescriptionExportIntent.Schema, value, ct).ConfigureAwait(false);
                var accepted = await processes.AcceptIfAbsentAsync(new(request.TenantId, request.ProgramId, request.DescriptionId, id, binding, intent,
                    "accepted", 0, 0, null, null, null), ct).ConfigureAwait(false);
                if (!accepted.IsSuccess)
                    return Result.Failure(accepted.Error!);
                record = accepted.Value;
            }
            return await ResumeAsync(record, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            try
            {
                var current = await processes.ReadAsync(request.TenantId, id, request.CommandId, ct).ConfigureAwait(false);
                if (current is not null && Matches(current, binding, request.ProgramId, request.DescriptionId, id))
                {
                    if (current.State == "completed")
                    {
                        try
                        { return await ReplayAsync(current, ct).ConfigureAwait(false); }
                        catch (ImmutableContentIntegrityException replayFailure) { return Result.Failure(SystemDescriptionReadFailure.Of(replayFailure)); }
                    }
                    if (exception is not ImmutableContentUnavailableException && current.State != "failed")
                    {
                        var closed = await processes.ReplaceAsync(current, current with { State = "failed", FailureCode = "source_integrity" }, ct).ConfigureAwait(false);
                        if (closed.IsSuccess && closed.Value.State == "completed")
                            return await ReplayAsync(closed.Value, ct).ConfigureAwait(false);
                    }
                }
                return Result.Failure(SystemDescriptionReadFailure.Of(exception));
            }
            catch (Exception recoveryFailure) when (!ct.IsCancellationRequested && Retryability.IsRetryable(recoveryFailure))
            {
                return Failure(RequestErrorKind.Conflict, "The export result recovery is pending. Retry the original operation.", true);
            }
        }
        catch (Exception exception) when (!ct.IsCancellationRequested && Retryability.IsRetryable(exception))
        {
            // A lost storage/commit response never reselects live content; the next invocation resumes this same accepted process.
            try
            {
                var recovered = await processes.ReadAsync(request.TenantId, id, request.CommandId, ct).ConfigureAwait(false);
                if (recovered is not null && Matches(recovered, binding, request.ProgramId, request.DescriptionId, id) && recovered.State == "completed")
                    return await ReplayAsync(recovered, ct).ConfigureAwait(false);
            }
            catch (Exception recoveryFailure) when (!ct.IsCancellationRequested && Retryability.IsRetryable(recoveryFailure))
            {
                // Outage during recovery is still pending; a later invocation resumes the sealed original intent.
            }
            return Failure(RequestErrorKind.Conflict, "The export storage outcome is pending. Retry the original operation.", true);
        }
    }
    async ValueTask<Result> ResolveFailureAsync(GenerateSystemDescriptionExport request, AcceptedOperationBinding binding, Uuid id,
        RequestError failure, CancellationToken ct)
    {
        var current = await processes.ReadAsync(request.TenantId, id, request.CommandId, ct).ConfigureAwait(false);
        if (current is null)
            return Result.Failure(failure);
        return !Matches(current, binding, request.ProgramId, request.DescriptionId, id)
            ? Failure(RequestErrorKind.Conflict, "The export identity is bound to another exact operation.")
            : await ResumeAsync(current, ct).ConfigureAwait(false);
    }
    async ValueTask<Result> ResumeAsync(SystemDescriptionExportProcessRecord record, CancellationToken ct)
    {
        if (record.State == "completed")
            return await ReplayAsync(record, ct).ConfigureAwait(false);
        if (record.State == "failed")
            return TechnicalFailure(record.FailureCode);
        var attempt = await processes.ReplaceAsync(record, record with { State = "running", Attempts = record.Attempts + 1 }, ct).ConfigureAwait(false);
        if (!attempt.IsSuccess)
            return Result.Failure(attempt.Error!);
        record = attempt.Value;
        if (record.State == "completed")
            return await ReplayAsync(record, ct).ConfigureAwait(false);
        if (record.State == "failed")
            return TechnicalFailure(record.FailureCode);
        var intent = await ReadIntentAsync(record, ct).ConfigureAwait(false);
        var exact = await content.ReadVerifiedAsync(record.TenantId.ToString(), intent.SourceContent, ct).ConfigureAwait(false);
        var version = await content.ReadJsonAsync<SystemDescriptionVersionView>(record.TenantId.ToString(), intent.SourceContent, ct).ConfigureAwait(false);
        if (intent.SourceContent.Schema != SystemDescriptionContent.CurrentSchema || version.TenantId != intent.TenantId || version.ProgramId != intent.ProgramId ||
            version.SystemDescriptionId != intent.DescriptionId || version.SystemDescriptionVersionId != intent.VersionId || version.Version != intent.Version ||
            version.Status != "approved" || version.ContentSha256 != intent.ContentSha256 ||
            SystemDescriptionContentIdentity.Digest(version.Scope, version.Content) != intent.ContentSha256)
            throw new ImmutableContentIntegrityException("The accepted export source differs from its exact approved version and content.");
        var rendered = SystemDescriptionDocumentRenderer.Render(intent, version, exact);
        if (!rendered.IsSuccess)
        {
            var failed = await processes.ReplaceAsync(record, record with { State = "failed", FailureCode = rendered.Error!.Message }, ct).ConfigureAwait(false);
            return !failed.IsSuccess ? Result.Failure(failed.Error!) : failed.Value.State == "completed"
                ? await ReplayAsync(failed.Value, ct).ConfigureAwait(false) : TechnicalFailure(failed.Value.FailureCode);
        }
        var document = rendered.Value;
        await using var bytes = new MemoryStream(document.Bytes, writable: false);
        var stored = await artifacts.StoreIfAbsentAsync(record.TenantId, bytes, ct).ConfigureAwait(false);
        if (stored.Content.TenantId != record.TenantId || stored.Content.Length != document.Bytes.LongLength ||
            stored.Content.Sha256 != ContentDigest.Sha256(document.Bytes))
            throw new ImmutableContentIntegrityException("The generated export differs from its verified artifact identity.");
        await using var opened = await artifacts.OpenVerifiedAsync(stored.Content, ct).ConfigureAwait(false);
        if (opened is null)
            throw new ImmutableContentUnavailableException("The generated export is not readable for verification yet.");
        if (!Uuid.TryParse(intent.Operation.Actor.Id, out var member))
            throw new ImmutableContentIntegrityException("The accepted export has another actor kind.");
        var summary = new SystemDescriptionExportView(intent.Operation.LogicalId, intent.VersionId, intent.Version, intent.Format, intent.ExportKey,
            intent.ContentSha256, member, intent.Operation.Actor.Display, intent.Operation.AcceptedAt);
        var output = new SystemDescriptionExportOutput(intent.TenantId, intent.ProgramId, intent.DescriptionId, intent.ProcessId, intent.VersionId, intent.Version,
            intent.Format, intent.ExportKey, intent.ContentSha256, intent.SourceContent, intent.VersionSource, intent.ApprovalSource, stored.Content, document.MediaType, summary);
        var outputRef = await content.WriteJsonAsync(record.TenantId.ToString(), Uuid.CreateVersion5(record.ProcessId, "output:" + stored.Content.Sha256),
            SystemDescriptionExportOutput.Schema, output, ct).ConfigureAwait(false);
        var unit = await content.WriteJsonAsync(record.TenantId.ToString(), Uuid.CreateVersion5(record.ProcessId, "unit-body"), UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        var result = await content.WriteJsonAsync(record.TenantId.ToString(), Uuid.CreateVersion5(record.ProcessId, "unit-result"), "mutation-result-content.v1",
            new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct).ConfigureAwait(false);
        var completed = await processes.ReplaceAsync(record, record with { State = "completed", Output = outputRef, OperationResult = result }, ct).ConfigureAwait(false);
        if (!completed.IsSuccess)
            return Result.Failure(completed.Error!);
        return completed.Value.State == "completed" ? await ReplayAsync(completed.Value, ct).ConfigureAwait(false) : TechnicalFailure(completed.Value.FailureCode);
    }
    async ValueTask<SystemDescriptionExportIntent> ReadIntentAsync(SystemDescriptionExportProcessRecord record, CancellationToken ct)
    {
        if (record.Intent.Schema != SystemDescriptionExportIntent.Schema)
            throw new ImmutableContentIntegrityException("The technical export intent has another schema.");
        var intent = await content.ReadJsonAsync<SystemDescriptionExportIntent>(record.TenantId.ToString(), record.Intent, ct).ConfigureAwait(false);
        if (intent.TenantId != record.TenantId || intent.ProgramId != record.ProgramId || intent.DescriptionId != record.DescriptionId || intent.ProcessId != record.ProcessId ||
            AcceptedOperationBinding.Of(intent.Operation) != record.Binding || intent.VersionSource.Stream.Realm != record.TenantId.ToString() ||
            intent.ApprovalSource.Stream != new EventStreamAddress(record.TenantId.ToString(), SystemDescription.Area, record.DescriptionId.ToString()) ||
            intent.VersionSource.Stream != intent.ApprovalSource.Stream || intent.VersionSource.EventId == Uuid.Empty || intent.ApprovalSource.EventId == Uuid.Empty ||
            intent.VersionSource.AggregateVersion == 0 || intent.ApprovalSource.AggregateVersion == 0)
            throw new ImmutableContentIntegrityException("The technical export intent differs from its exact admitted owner and native source proofs.");
        return intent;
    }
    async ValueTask<Result> ReplayAsync(SystemDescriptionExportProcessRecord record, CancellationToken ct)
    {
        var intent = await ReadIntentAsync(record, ct).ConfigureAwait(false);
        if (record.Output is not { Schema: SystemDescriptionExportOutput.Schema } outputRef ||
            record.OperationResult is not { Schema: "mutation-result-content.v1" } resultRef)
            throw new ImmutableContentIntegrityException("The completed export has no exact immutable output and original result.");
        var output = await content.ReadJsonAsync<SystemDescriptionExportOutput>(record.TenantId.ToString(), outputRef, ct).ConfigureAwait(false);
        if (output.TenantId != intent.TenantId || output.ProgramId != intent.ProgramId || output.DescriptionId != intent.DescriptionId || output.ProcessId != intent.ProcessId ||
            output.VersionId != intent.VersionId || output.Version != intent.Version || output.Format != intent.Format || output.ExportKey != intent.ExportKey ||
            output.ContentSha256 != intent.ContentSha256 || output.SourceContent != intent.SourceContent || output.VersionSource != intent.VersionSource ||
            output.ApprovalSource != intent.ApprovalSource || output.Artifact.TenantId != record.TenantId ||
            output.Artifact.Length <= 0 || output.Artifact.Sha256 is not { Length: 64 } ||
            output.MediaType != (intent.Format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document") ||
            output.Export.ExportId != intent.Operation.LogicalId || output.Export.VersionId != intent.VersionId || output.Export.Version != intent.Version ||
            output.Export.Format != intent.Format || output.Export.ExportKey != intent.ExportKey || output.Export.ContentSha256 != intent.ContentSha256 ||
            output.Export.GeneratedByMemberId.ToString() != intent.Operation.Actor.Id || output.Export.GeneratedByDisplay != intent.Operation.Actor.Display ||
            output.Export.GeneratedAt != intent.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The completed export output differs from its original accepted source.");
        var result = await content.ReadJsonAsync<MutationResultContent>(record.TenantId.ToString(), resultRef, ct).ConfigureAwait(false);
        if (result.Schema != UnitResult.Schema || result.Value is not StandardResponseBody body || body.Content.Schema != UnitResult.Schema)
            throw new ImmutableContentIntegrityException("The original export terminal result has another schema.");
        await content.ReadJsonAsync<UnitResult>(record.TenantId.ToString(), body.Content, ct).ConfigureAwait(false);
        return Result.Success;
    }
    static bool Matches(SystemDescriptionExportProcessRecord record, AcceptedOperationBinding binding, Uuid program, Uuid description, Uuid id) =>
        record.Binding == binding && record.ProgramId == program && record.DescriptionId == description && record.ProcessId == id;
    static Result TechnicalFailure(string? code) => Failure(code == "source_integrity" ? RequestErrorKind.Conflict : RequestErrorKind.Validation,
        code ?? "The admitted export has a permanent technical failure.");
    static Result Failure(RequestErrorKind kind, string message, bool transient = false) => Result.Failure(new RequestError(kind, message, transient));
}
