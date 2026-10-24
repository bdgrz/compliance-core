using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The content rules shared by the finding and corrective action owners and their admission handlers.</summary>
static class FindingRules
{
    public const int MaximumTextLength = 4000;

    static readonly string[] SourceKinds = ["manual", "readiness_gap", "control_occurrence",
        "occurrence_review", "evaluation_deviation", "incident", "consultant_observation",
        "examination_item"];
    static readonly string[] LinkKinds = ["control", "criterion", "evidence", "access_decision",
        "risk", "vendor", "readiness_gap", "control_occurrence", "control_evaluation"];
    static readonly string[] Severities = ["low", "medium", "high", "critical"];

    /// <summary>The deterministic id of the default action a routed finding admits with its raise.</summary>
    public static Uuid RoutedActionId(Uuid findingId) => Uuid.CreateVersion5(findingId, "corrective-action");

    public static CommandFailure? ValidateRaise(FindingSource? source, string? title, string? description,
        string? severity, string? affectedScope, Uuid ownerMemberId, DateOnly dueOn, IReadOnlyList<FindingLink>? links)
    {
        if (links is null)
            return InvalidLinks();
        if (source is null || !SourceKinds.Contains(source.Kind) ||
            string.IsNullOrWhiteSpace(source.SourceText) || source.SourceText.Length > 8000 ||
            source.Version is { Length: > 200 } ||
            source.Kind is not ("manual" or "incident" or "consultant_observation") && source.RecordId is null)
            return CommandFailure.InvalidContent(
                "A finding source needs a known kind, the source record for a governed source, and source text of at most 8000 characters.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 ||
            !IsBoundedText(description, 8000) || !IsBoundedText(affectedScope, 2000))
            return CommandFailure.InvalidContent(
                "A finding needs a title of at most 200 characters, a description, and an affected scope.");
        if (ValidateTriage(severity, ownerMemberId, dueOn) is { } triage)
            return triage;
        return links.Count > 50 || links.Any(static link => link is null ||
                !LinkKinds.Contains(link.Kind) || string.IsNullOrWhiteSpace(link.Reference) ||
                link.Reference.Length > 500)
            ? InvalidLinks()
            : null;
    }

    public static CommandFailure? ValidateTriage(string? severity, Uuid ownerMemberId, DateOnly dueOn) =>
        severity is null || !Severities.Contains(severity) || ownerMemberId == Uuid.Empty || dueOn == default
            ? CommandFailure.InvalidContent(
                "A finding needs a severity of low, medium, high, or critical, an owner, and a due date.")
            : null;

    public static CommandFailure? ValidateRevision(string? severity, Uuid ownerMemberId, DateOnly dueOn,
        string? affectedScope, string? rootCause, string? reason)
    {
        if (ValidateTriage(severity, ownerMemberId, dueOn) is { } triage)
            return triage;
        return !IsBoundedText(affectedScope, 2000) || rootCause is { } cause &&
            cause.Trim().Length > MaximumTextLength || !IsBoundedText(reason, MaximumTextLength)
            ? CommandFailure.InvalidContent(
                "A finding revision needs an affected scope, a root cause of at most 4000 characters, and a reason.")
            : null;
    }

    public static CommandFailure? ValidateAction(Uuid actionId, string? description, Uuid ownerMemberId, DateOnly dueOn)
    {
        if (actionId == Uuid.Empty)
            return CommandFailure.InvalidContent("A corrective action needs a nonempty identity.");
        return !IsBoundedText(description, MaximumTextLength) || ownerMemberId == Uuid.Empty || dueOn == default
            ? CommandFailure.InvalidContent(
                "A corrective action needs a description of at most 4000 characters, an owner, and a due date.")
            : null;
    }

    public static CommandFailure? ValidateCompletion(string? resolutionNotes, IReadOnlyList<EvidenceReference>? evidence) =>
        evidence is null
            ? CommandFailure.InvalidContent("The finding requires valid evidence references.")
            : !IsBoundedText(resolutionNotes, MaximumTextLength) || EvidenceProblem(evidence) is not null
                ? CommandFailure.InvalidContent(
                    "Completing a corrective action needs resolution notes and at least one valid evidence reference.")
                : null;

    public static CommandFailure? ValidateClosure(string? verificationRationale,
        IReadOnlyList<EvidenceReference>? resolutionEvidence, string? rationale) =>
        resolutionEvidence is null
            ? CommandFailure.InvalidContent("The finding requires valid evidence references.")
            : EvidenceProblem(resolutionEvidence) is not null || !IsBoundedText(verificationRationale, MaximumTextLength) ||
              !IsBoundedText(rationale, MaximumTextLength)
                ? CommandFailure.InvalidContent(
                    "Closing a finding requires resolution evidence, a verification rationale, and a closure rationale.")
                : null;

    public static string? EvidenceProblem(IReadOnlyList<EvidenceReference> evidence) =>
        evidence.Count is < 1 or > 50 || evidence.Any(static item => item is null ||
            item.Kind is not ("artifact" or "record" or "external") ||
            string.IsNullOrWhiteSpace(item.Reference) || item.Reference.Length > 2000 ||
            item.Description is { Length: > 2000 })
            ? "invalid"
            : null;

    public static EvidenceReference[] AsUnresolved(IReadOnlyList<EvidenceReference> evidence) =>
        evidence.Select(static item => new EvidenceReference(item.ExpectedEvidenceIndex, item.Kind,
            item.Reference.Trim(), string.IsNullOrWhiteSpace(item.Description)
                ? null
                : item.Description.Trim(), "unresolved")).ToArray();

    public static IReadOnlyList<FindingLink> Trimmed(IReadOnlyList<FindingLink> links) =>
        links.Select(static link => link with { Reference = link.Reference.Trim() }).ToArray();

    public static bool IsBoundedText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static CommandFailure InvalidLinks() => CommandFailure.InvalidContent(
        "Each finding link needs a known kind and a reference of at most 500 characters.");
}
