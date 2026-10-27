using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Freezes and reads the completed response of a direct team assignment or removal.</summary>
static class TeamMemberResponses
{
    /// <summary>Writes the completed response content and describes it as the HTTP 200 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeCompletedAsync(IImmutableContentStore content,
        string realm, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var response = new AdministratorTransitionResponse(operationId, AdministratorTransitionResponseState.Completed,
            null, acceptedAt);
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AdministratorTransitionResponse.Schema,
            response, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
            AdministratorTransitionResponse.Schema, new StandardResponseBody(body));
    }

    /// <summary>The response a committed or replayed acceptance froze, read back as verified content.</summary>
    public static async Task<Result<AdministratorTransitionResponse>> ReadAsync(IImmutableContentStore content,
        string realm, Result<AcceptedAppendResult> accepted, CancellationToken ct)
    {
        if (!accepted.IsSuccess)
            return Result<AdministratorTransitionResponse>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<AdministratorTransitionResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body ||
            descriptor.ResultSchema != AdministratorTransitionResponse.Schema)
            throw new ImmutableContentIntegrityException("The accepted team operation froze an unexpected response.");
        return Result<AdministratorTransitionResponse>.Success(
            await content.ReadJsonAsync<AdministratorTransitionResponse>(realm, body.Content, ct).ConfigureAwait(false));
    }
}
