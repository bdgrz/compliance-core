using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Admits the creation of a location as the first native fact on its own record owner. The location identity is
///     the logical identity of the creation, so an exact retry replays the original 202 even after later changes; the
///     active name within the location kind is claimed by the name-change process that follows.
/// </summary>
public sealed class RecordLocationHandler(NamedRecordAdmission admission, TechnologyInventoryReferences references)
    : IRequestHandler<RecordLocation, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<RecordLocation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.LocationId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A location identity and tenant are required."));
        return await admission.AdmitAsync(LocationNames.Descriptor, context, new NamedRecordAdmissionRequest(
            request.TenantId, request.LocationId, request.LocationId, true, 0, TechnologyInventoryActor.From(context),
            AuthoredIntentDigest.Compute(request), token => ResolveAsync(request, token)),
            (Location record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct).ConfigureAwait(false);
    }

    async ValueTask<Result<ReadOnlyMemory<byte>>> ResolveAsync(RecordLocation request, CancellationToken ct) =>
        await LocationContents.ResolveAsync(references, request.TenantId, new LocationContent(request.Kind,
            request.Name, request.GeographyReference, request.OwnerPersonId, TechnologyInventoryRules.Active), ct)
            .ConfigureAwait(false);
}

/// <summary>Normalizes, validates and serializes the complete location content a change freezes.</summary>
static class LocationContents
{
    public static async ValueTask<Result<ReadOnlyMemory<byte>>> ResolveAsync(TechnologyInventoryReferences references,
        Uuid tenantId, LocationContent authored, CancellationToken ct)
    {
        var content = InventoryRegisterRules.Normalize(authored);
        if (InventoryRegisterRules.Validate(content) is { } error)
            return Result<ReadOnlyMemory<byte>>.Failure(new RequestError(RequestErrorKind.Validation, error));
        if (await references.ValidateOwnerAsync(tenantId, content.OwnerPersonId, ct).ConfigureAwait(false) is { } owner)
            return Result<ReadOnlyMemory<byte>>.Failure(owner);
        return Result<ReadOnlyMemory<byte>>.Success(
            JsonSerializer.SerializeToUtf8Bytes(content, ComplianceCoreJsonContext.Default.LocationContent));
    }
}
