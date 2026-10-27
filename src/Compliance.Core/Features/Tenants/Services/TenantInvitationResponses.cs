using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>Freezes the immediate Unit response of the invitation commands.</summary>
static class TenantInvitationResponses
{
    /// <summary>Writes the Unit result content and describes it as the HTTP 200 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeUnitAsync(IImmutableContentStore content, string realm,
        Uuid logicalId, CancellationToken ct)
    {
        var result = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(logicalId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(result));
    }

    /// <summary>The result of a closed acceptance outcome: committed and replayed succeed, a changed intent conflicts.</summary>
    public static Result Of(Result<AcceptedAppendResult> accepted) =>
        !accepted.IsSuccess
            ? Result.Failure(accepted.Error!)
            : accepted.Value is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed
                ? Result.Success
                : Result.Failure(AcceptedAppendResult.ConflictError);
}
