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
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Admits the creation of a provider as the first native fact on its own record owner. The provider identity is
///     the logical identity of the creation, so an exact retry replays the original 202 even after later changes; the
///     governed name is claimed by the name-change process that follows.
/// </summary>
public sealed class RecordProviderHandler(NamedRecordAdmission admission, ProviderReferences references)
    : IRequestHandler<RecordProvider, OperationAcceptedResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<RecordProvider> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.ProviderId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider identity and tenant are required."));
        if (ProviderRules.InputError(request.Content) is { } inputError)
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Validation, inputError));
        return await admission.AdmitAsync(ProviderNames.Descriptor, context, new NamedRecordAdmissionRequest(
            request.TenantId, request.ProviderId, request.ProviderId, true, 0, ProviderActor.From(context),
            AuthoredIntentDigest.Compute(request), token => ProviderContents.ResolveAsync(references,
                request.TenantId, request.Content, token)),
            (Provider record, AcceptAttempt change) => record.AcceptChange(change.Operation, change.Create,
                change.ExpectedRevision, change.Intent, change.FirstResponse), ct).ConfigureAwait(false);
    }
}

/// <summary>Resolves the authored provider content into the normalized document a change freezes.</summary>
static class ProviderContents
{
    public static async ValueTask<Result<ReadOnlyMemory<byte>>> ResolveAsync(ProviderReferences references,
        Uuid tenantId, ProviderContent authored, CancellationToken ct)
    {
        var resolved = await references.ResolveAsync(tenantId, authored, ct).ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result<ReadOnlyMemory<byte>>.Failure(resolved.Error!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(resolved.Value!, ComplianceCoreJsonContext.Default.ProviderContent);
        return bytes.Length > Provider.MaximumContentBytes
            ? Result<ReadOnlyMemory<byte>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider declaration exceeds the bounded content size."))
            : Result<ReadOnlyMemory<byte>>.Success(bytes);
    }
}
