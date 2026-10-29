using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Processes;

/// <summary>What the native admissions of the email address commands share: the actor, the provenance and the frozen Unit response.</summary>
static class EmailAddressOperations
{
    /// <summary>The realm every email address lives in.</summary>
    public const string Realm = "bdgrz";

    /// <summary>The original actor of an admission: the system process or the authenticated user.</summary>
    public static ActorReference? ActorOf(ClaimsPrincipal principal)
    {
        if (RequestActor.IsSystem(principal))
            return ActorReference.ForSystemProcess("system", "system");
        return UserIdentityClaims.TryGetBdgrzSubject(principal, out var userId)
            ? new ActorReference("user", userId.ToString(), UserIdentityClaims.BdgrzDisplay(principal, userId))
            : null;
    }

    /// <summary>The provenance of a root admission on <paramref name="owner" />; a retry's binding is derived from it.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, ActorReference actor,
        EventStreamAddress owner, long expectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(logicalId, logicalId, null, commandKind, actor, owner, logicalId.ToString(),
            expectedRevision >= 0 ? (ulong)expectedRevision : null, authoredIntentSha256, acceptedAt, owner, owner);

    /// <summary>Freezes the Unit response of an admission as verified immutable content.</summary>
    public static async Task<FirstResponseDescriptor> UnitResponseAsync(IImmutableContentStore content,
        CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), UnitResult.Schema, new UnitResult(), ct)
            .ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema,
            new StandardResponseBody(unit));
    }

    /// <summary>The closed outcome of a root admission as a handler result.</summary>
    public static Result Of(Result<AcceptedAppendResult> accepted) => !accepted.IsSuccess
        ? Result.Failure(accepted.Error!)
        : accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
}
