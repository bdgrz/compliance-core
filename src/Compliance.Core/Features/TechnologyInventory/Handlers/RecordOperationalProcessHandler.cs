using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Admits the creation of an operational process as the first native fact on its own record owner. The process
///     identity is the logical identity of the creation, so an exact retry replays the original 202 even after later
///     changes; the active name is claimed by the name-change process that follows.
/// </summary>
public sealed class RecordOperationalProcessHandler(NamedRecordAdmission admission,
    TechnologyInventoryReferences references)
    : IRequestHandler<RecordOperationalProcess, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordOperationalProcess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.OperationalProcessId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "An operational process identity and tenant are required."));
        return await admission.AdmitAsync(OperationalProcessNames.Descriptor, context,
            new NamedRecordAdmissionRequest(request.TenantId, request.OperationalProcessId,
                request.OperationalProcessId, true, 0, TechnologyInventoryActor.From(context),
                AuthoredIntentDigest.Compute(request), token => ResolveAsync(request, token)),
            (OperationalProcess record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<ReadOnlyMemory<byte>>> ResolveAsync(RecordOperationalProcess request,
        CancellationToken ct) =>
        await OperationalProcessContents.ResolveAsync(references, request.TenantId, new OperationalProcessContent(
            request.Name, request.Purpose, request.OwnerPersonId, request.Inputs, request.Outputs,
            TechnologyInventoryRules.Active), ct).ConfigureAwait(false);
}

/// <summary>Normalizes, validates and serializes the complete operational process content a change freezes.</summary>
static class OperationalProcessContents
{
    public static async ValueTask<Result<ReadOnlyMemory<byte>>> ResolveAsync(
        TechnologyInventoryReferences references, Uuid tenantId, OperationalProcessContent authored,
        CancellationToken ct)
    {
        var content = InventoryRegisterRules.Normalize(authored);
        if (InventoryRegisterRules.Validate(content) is { } error)
            return Result<ReadOnlyMemory<byte>>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (await references.ValidateOwnerAsync(tenantId, content.OwnerPersonId, ct).ConfigureAwait(false) is { } owner)
            return Result<ReadOnlyMemory<byte>>.Failure(owner);
        return Result<ReadOnlyMemory<byte>>.Success(JsonSerializer.SerializeToUtf8Bytes(content,
            ComplianceCoreJsonContext.Default.OperationalProcessContent));
    }
}
