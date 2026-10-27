using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>Guards and result shaping shared by the risk governance command handlers.</summary>
static class RiskCommands
{
    public const int MaximumTextLength = 4000;
    public const int MaximumTitleLength = 200;

    /// <summary>The frozen unit response and its mutation result.</summary>
    public static Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeUnitAsync(
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct) =>
        FindingContent.FreezeAsync(content, tenantId, UnitResult.Schema, new UnitResult(), ct);

    /// <summary>The closed result of an accepted, replayed or conflicting unit operation.</summary>
    public static async ValueTask<Result> UnitAsync(IImmutableContentStore content, Uuid tenantId,
        Result<AcceptedAppendResult> outcome, CancellationToken ct)
    {
        var response = await FindingContent.ResponseAsync<UnitResult>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
        return response.IsSuccess ? Result.Success : Result.Failure(response.Error!);
    }

    /// <summary>True when the text is empty, whitespace or longer than the bound.</summary>
    public static bool InvalidText(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaximumTextLength;

    /// <summary>The distinct non-empty evidence request ids in authored order.</summary>
    public static List<Uuid> Distinct(IReadOnlyList<Uuid>? ids) =>
        [.. (ids ?? []).Where(static id => id != Uuid.Empty).Distinct()];

    /// <summary>Checks that every named evidence request is an open or fulfilled request of the program.</summary>
    public static async ValueTask<RequestError?> RequireCitableEvidenceAsync(IAggregateReader reader, Uuid tenantId,
        Uuid programId, IEnumerable<Uuid> ids, CancellationToken ct)
    {
        foreach (var id in ids)
        {
            var request = await reader.HydrateAsync(new EvidenceRequest(tenantId, programId, id), ct)
                .ConfigureAwait(false);
            if (request.Status is not (EvidenceRequest.Open or EvidenceRequest.Fulfilled))
                return new RequestError(RequestErrorKind.Validation,
                    "Every evidence request must be an open or fulfilled request in this program.");
        }

        return null;
    }

    /// <summary>The validation failure of the content of an action, or null.</summary>
    public static CommandFailure? ValidateAction(string? title, string? targetState, string? expectedEvidence,
        DateOnly dueOn, Uuid accountableMemberId, DateTimeOffset now)
    {
        if (accountableMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(title) ||
            title.Trim().Length > MaximumTitleLength || InvalidText(targetState) || InvalidText(expectedEvidence))
            return CommandFailure.InvalidContent(
                "A treatment action requires a title of at most 200 characters, a target state, expected evidence, and an accountable member.");
        return dueOn < DateOnly.FromDateTime(now.UtcDateTime)
            ? CommandFailure.InvalidContent("A treatment action cannot be due in the past.")
            : null;
    }
}
