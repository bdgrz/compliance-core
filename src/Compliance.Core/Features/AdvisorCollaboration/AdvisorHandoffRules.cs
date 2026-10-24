using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Request-level validation shared by the handoff and feedback commands.</summary>
static class AdvisorHandoffRules
{
    public static readonly IReadOnlySet<string> FeedbackKinds = new HashSet<string>(StringComparer.Ordinal)
        { "comment", "change_request", "validated" };

    public static bool ValidSelection(IReadOnlyList<AdvisorRecordVersion>? selection) =>
        selection is { Count: > 0 and <= AdvisorHandoff.MaximumSelectionCount } &&
        selection.All(static item => item is not null && item.HasValidIdentity) &&
        selection.Select(static item => (item.SourceFamily, item.RecordId, item.EntryId))
            .Distinct().Count() == selection.Count;

    public static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    public static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    public static Result NotFound() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The advisor handoff was not found in the requested program."));

    public static Result Forbidden(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Forbidden, message));

    /// <summary>The handoff snapshot is created and belongs to the requested program.</summary>
    public static bool InProgram(AdvisorHandoff handoff, Uuid programId) =>
        handoff.IsCreated && handoff.ProgramId == programId;
}
