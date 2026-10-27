using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

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
