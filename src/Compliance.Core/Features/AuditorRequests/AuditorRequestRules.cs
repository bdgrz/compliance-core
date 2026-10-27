using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using System.Text;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The content rules shared by the request, response, thread and selection owners and their admission handlers.
///     Lifetime caps on comments, responses, versions and selections no longer exist; only the bounds of one piece of
///     authored content remain.
/// </summary>
static class AuditorRequestRules
{
    public const int MaximumBodyLength = 8_000;
    public const int MaximumSummaryLength = 12_000;
    public const int MaximumRationaleLength = 2_000;
    public const int MaximumReferenceLength = 500;
    public const int MaximumEvidenceArtifacts = 64;
    public const int MaximumSampleRows = 500;

    static readonly HashSet<string> Sources = new(StringComparer.Ordinal)
    {
        "external_email", "external_portal", "in_product_attest",
    };

    static readonly HashSet<string> Priorities = new(StringComparer.Ordinal) { "low", "normal", "high", "urgent" };
    static readonly HashSet<string> Compartments = new(StringComparer.Ordinal) { "internal", "attest_shared" };

    static readonly HashSet<string> SampleMethods = new(StringComparer.Ordinal)
    {
        "judgmental", "random", "stratified", "targeted",
    };

    static readonly HashSet<string> Dispositions = new(StringComparer.Ordinal)
    {
        "accepted", "rejected", "changes_requested",
    };

    public static string CleanText(string? value) => (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);

    public static string CleanToken(string? value) => CleanText(value).ToLowerInvariant();

    public static string? NormalizeOptional(string? value)
    {
        var normalized = CleanText(value);
        return normalized.Length == 0 ? null : normalized;
    }

    public static bool Bounded(string? value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum &&
        (minimum == 0 || !string.IsNullOrWhiteSpace(value));

    public static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);

    public static RequestError Failure(CommandFailure failure) => failure.ToRequestError();

    /// <summary>The normalized terms of a new request, or the reason they are unsupported.</summary>
    public static Result<AuditorRequestTermsContent> RecordTerms(RecordAuditorRequest request, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset at)
    {
        var source = CleanToken(request.Source);
        var externalReference = NormalizeOptional(request.ExternalReference);
        var wording = CleanText(request.Wording);
        var scope = CleanText(request.Scope);
        var priority = CleanToken(request.Priority);
        if (request.ExaminationId == Uuid.Empty || !Sources.Contains(source) ||
            source != "in_product_attest" && externalReference is null ||
            externalReference is { Length: > MaximumReferenceLength } ||
            !Bounded(wording, 1, MaximumBodyLength) || !Bounded(scope, 1, MaximumBodyLength) ||
            !Priorities.Contains(priority) || request.OwnerMemberId == Uuid.Empty || request.DueOn == default)
            return Result<AuditorRequestTermsContent>.Failure(Failure(Invalid(
                "The auditor request terms are incomplete or unsupported.")));
        return Result<AuditorRequestTermsContent>.Success(new AuditorRequestTermsContent(request.ProgramId, request.ExaminationId, source,
            externalReference ?? string.Empty, wording, scope, priority, request.OwnerMemberId, request.DueOn, true,
            string.Empty, actorMemberId, actorDisplay, at));
    }

    /// <summary>The terms after a revision of wording, scope, priority and due date.</summary>
    public static Result<AuditorRequestTermsContent> ReviseTerms(AuditorRequestTermsContent current,
        ReviseAuditorRequest request, Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        var wording = CleanText(request.Wording);
        var scope = CleanText(request.Scope);
        var priority = CleanToken(request.Priority);
        var reason = CleanText(request.ChangeReason);
        if (!Bounded(wording, 1, MaximumBodyLength) || !Bounded(scope, 1, MaximumBodyLength) ||
            !Priorities.Contains(priority) || request.DueOn == default || !Bounded(reason, 1, MaximumRationaleLength))
            return Result<AuditorRequestTermsContent>.Failure(Failure(Invalid(
                "A request revision needs bounded wording, scope, priority, due date, and reason.")));
        return Result<AuditorRequestTermsContent>.Success(current with
        {
            Wording = wording,
            Scope = scope,
            Priority = priority,
            DueOn = request.DueOn,
            Reason = reason,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay,
            At = at,
        });
    }

    /// <summary>The terms after an assignment to a new owner and due date.</summary>
    public static Result<AuditorRequestTermsContent> AssignTerms(AuditorRequestTermsContent current,
        AssignAuditorRequest request, Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        var reason = CleanText(request.AssignmentReason);
        if (request.OwnerMemberId == Uuid.Empty || request.DueOn == default ||
            !Bounded(reason, 1, MaximumRationaleLength))
            return Result<AuditorRequestTermsContent>.Failure(Failure(Invalid(
                "Assignment requires an owner, due date, and bounded rationale.")));
        return Result<AuditorRequestTermsContent>.Success(current with
        {
            OwnerMemberId = request.OwnerMemberId,
            DueOn = request.DueOn,
            Reason = reason,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay,
            At = at,
        });
    }

    public static string? ReopenReason(string? reason)
    {
        var normalized = CleanText(reason);
        return Bounded(normalized, 1, MaximumRationaleLength) ? normalized : null;
    }

    /// <summary>The normalized comment body and compartment, or null when unsupported.</summary>
    public static (string Body, string Compartment)? Comment(string? body, string? compartment)
    {
        var text = CleanText(body);
        var token = CleanToken(compartment);
        return Bounded(text, 1, MaximumBodyLength) && Compartments.Contains(token) ? (text, token) : null;
    }

    /// <summary>The normalized response summary and evidence, or null when unsupported.</summary>
    public static (string Summary, IReadOnlyList<Uuid> EvidenceArtifactIds)? Response(string? summary,
        IReadOnlyList<Uuid>? evidenceArtifactIds)
    {
        var text = CleanText(summary);
        if (!Bounded(text, 1, MaximumSummaryLength) || evidenceArtifactIds is null ||
            evidenceArtifactIds.Count > MaximumEvidenceArtifacts || evidenceArtifactIds.Any(static id => id == Uuid.Empty) ||
            evidenceArtifactIds.Distinct().Count() != evidenceArtifactIds.Count)
            return null;
        return (text, Frozen(evidenceArtifactIds));
    }

    public static string? SubmissionNote(string? note)
    {
        var normalized = CleanText(note);
        return Bounded(normalized, 1, MaximumRationaleLength) ? normalized : null;
    }

    /// <summary>The normalized review outcome and rationale, or null when unsupported.</summary>
    public static (string Outcome, string Rationale)? Review(string? outcome, string? rationale)
    {
        var token = CleanToken(outcome);
        var text = CleanText(rationale);
        return token is "approved" or "rejected" && Bounded(text, 1, MaximumRationaleLength) ? (token, text) : null;
    }

    /// <summary>The normalized recipient and delivery reference, or null when unsupported.</summary>
    public static (string Recipient, string Reference)? Delivery(string? recipient, string? reference)
    {
        var to = CleanText(recipient);
        var by = CleanText(reference);
        return Bounded(to, 1, MaximumReferenceLength) && Bounded(by, 1, MaximumReferenceLength) ? (to, by) : null;
    }

    /// <summary>The normalized disposition outcome and optional external reference, or null when unsupported.</summary>
    public static (string Outcome, string? ExternalReference)? Disposition(string? outcome, string? externalReference)
    {
        var token = CleanToken(outcome);
        var reference = NormalizeOptional(externalReference);
        return Dispositions.Contains(token) && reference is not { Length: > MaximumReferenceLength }
            ? (token, reference)
            : null;
    }

    /// <summary>The normalized selection terms, or the reason they are unsupported.</summary>
    public static Result<SelectionTerms> Selection(string? method, string? source, string? externalReference,
        string? amendmentReason, bool amendment, IReadOnlyList<Uuid>? rowIds)
    {
        var token = CleanToken(method);
        var provenance = CleanToken(source);
        var reference = CleanText(externalReference);
        var reason = string.IsNullOrWhiteSpace(amendmentReason) ? null : CleanText(amendmentReason);
        var validProvenance = Sources.Contains(provenance) &&
            (provenance == "in_product_attest" || Bounded(reference, 1, MaximumReferenceLength));
        var validAmendment = amendment ? reason is not null && Bounded(reason, 1, MaximumRationaleLength) : reason is null;
        if (!SampleMethods.Contains(token) || !validProvenance || !validAmendment || reference.Length > MaximumReferenceLength ||
            rowIds is null || rowIds.Count is 0 or > MaximumSampleRows || rowIds.Any(static id => id == Uuid.Empty) ||
            rowIds.Distinct().Count() != rowIds.Count)
            return Result<SelectionTerms>.Failure(Failure(Invalid(
                "A sample selection requires an exact frozen population, provenance, and unique bounded rows.")));
        return Result<SelectionTerms>.Success(new SelectionTerms(token, provenance, reference, reason, Frozen(rowIds)));
    }

    public static IReadOnlyList<Uuid> Frozen(IEnumerable<Uuid> ids) =>
        Array.AsReadOnly(ids.OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray());

    /// <summary>The normalized terms of a sample selection.</summary>
    public sealed record SelectionTerms(string Method, string Source, string ExternalReference, string? AmendmentReason,
        IReadOnlyList<Uuid> RowIds);
}
