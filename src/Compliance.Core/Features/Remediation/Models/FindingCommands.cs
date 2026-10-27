using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>Guards and result shaping shared by the finding and corrective action command handlers.</summary>
static class FindingCommands
{
    public static async ValueTask<bool> IsActiveMemberAsync(IAggregateReader reader, Uuid tenantId, Uuid memberId,
        CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct).ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && member.Affiliation != "firm_staff";
    }

    public static RequestError InactiveOwner() => new(RequestErrorKind.Validation,
        "The owner must be an active client member.");

    /// <summary>The first response of a prepared command, or a retryable conflict when the owner changed while it was prepared.</summary>
    public static Result<FirstResponseDescriptor> Prepared(Result guard, FirstResponseDescriptor? response) =>
        !guard.IsSuccess
            ? Result<FirstResponseDescriptor>.Failure(guard.Error!)
            : response is null
                ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The record changed while the command was prepared; reload and retry.", true))
                : Result<FirstResponseDescriptor>.Success(response);

    public static string Digest<T>(T request) where T : notnull => AuthoredIntentDigest.Compute(request);
}
