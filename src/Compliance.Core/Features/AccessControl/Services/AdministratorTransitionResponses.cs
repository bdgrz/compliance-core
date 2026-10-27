using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
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
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Freezes the accepted response of a guard-first administrator transition.</summary>
static class AdministratorTransitionResponses
{
    /// <summary>The HTTP status an accepted transition answers with while the process continues.</summary>
    public const int AcceptedStatus = 202;

    /// <summary>Writes the accepted response content and describes it as the HTTP 202 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeAcceptedAsync(IImmutableContentStore content,
        string realm, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var response = new AdministratorTransitionResponse(operationId, AdministratorTransitionResponseState.Accepted,
            null, acceptedAt);
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AdministratorTransitionResponse.Schema,
            response, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(AcceptedStatus, ImmutableContentStoreExtensions.JsonMediaType,
            AdministratorTransitionResponse.Schema, new StandardResponseBody(body));
    }
}
