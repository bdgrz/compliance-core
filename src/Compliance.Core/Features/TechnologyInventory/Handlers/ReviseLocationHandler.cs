using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Admits a revision of a location at its expected revision as the first native fact on the location's own
///     owner. <c>CommandId</c> is the logical identity, so an exact retry replays the original 202 after any later
///     change. The kind is immutable and carried over from the observed revision.
/// </summary>
public sealed class ReviseLocationHandler(NamedRecordAdmission admission, TechnologyInventoryReferences references,
    IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<ReviseLocation, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<ReviseLocation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.LocationId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, location identity and tenant are required."));
        if (request.ExpectedRevision < 1)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected location revision must be positive."));
        return await admission.AdmitAsync(LocationNames.Descriptor, context, new NamedRecordAdmissionRequest(
            request.TenantId, request.LocationId, request.CommandId, false, request.ExpectedRevision,
            TechnologyInventoryActor.From(context), AuthoredIntentDigest.Compute(request),
            token => ResolveAsync(request, token)),
            (Location record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct).ConfigureAwait(false);
    }

    async ValueTask<Result<ReadOnlyMemory<byte>>> ResolveAsync(ReviseLocation request, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new Location(request.TenantId, request.LocationId), ct)
            .ConfigureAwait(false);
        if (owner.Current is not { } current)
            return Result<ReadOnlyMemory<byte>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The location was not found."));
        var bytes = await content.ReadVerifiedAsync(request.TenantId.ToString(), current.Content, ct)
            .ConfigureAwait(false);
        var observed = JsonSerializer.Deserialize(bytes.Span, ComplianceCoreJsonContext.Default.LocationContent)
            ?? throw new ImmutableContentIntegrityException("The location content is empty.");
        return await LocationContents.ResolveAsync(references, request.TenantId, observed with
        {
            Name = request.Name,
            OwnerPersonId = request.OwnerPersonId,
            Lifecycle = request.Lifecycle,
            GeographyReference = request.GeographyReference,
        }, ct).ConfigureAwait(false);
    }
}
