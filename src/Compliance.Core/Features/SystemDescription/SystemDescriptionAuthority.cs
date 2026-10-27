using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Current write authority advances lightweight commitments without loading historical narrative blobs.</summary>
public static class SystemDescriptionAuthority
{
    public const string Schema = "system-description-current-authority.v1";
    public static SystemDescriptionCurrentAuthority FromView(SystemDescriptionView view, ImmutableContentRef current)
    {
        var id = view.CurrentVersion?.SystemDescriptionVersionId ?? Uuid.Empty;
        return new(current, view.Versions.Select(Token).ToArray(), view.Reviews.Select(Hash).ToArray(),
            view.Approvals.Select(Hash).ToArray(), view.NoChangeDecisions.Select(Hash).ToArray(), view.Bindings.Select(Hash).ToArray(),
            view.Approvals.Count == 0 ? null : view.Approvals[^1].VersionId,
            view.Reviews.Where(item => item.VersionId == id).ToArray(), view.Approvals.FirstOrDefault(item => item.VersionId == id),
            view.NoChangeDecisions.Where(item => item.VersionId == id).ToArray(), view.Bindings.Where(item => item.VersionId == id).ToArray());
    }
    public static SystemDescriptionCurrentAuthority Advance(SystemDescriptionCurrentAuthority? before, SystemDescriptionView prior,
        SystemDescriptionView after, object mutation, bool changed, ImmutableContentRef current)
    {
        if (before is null)
            return FromView(after, current);
        if (!changed)
            return before;
        var versions = before.Versions.ToList();
        var previous = prior.CurrentVersion ?? throw new ImmutableContentIntegrityException("The description has no prior current authority.");
        var next = after.CurrentVersion ?? throw new ImmutableContentIntegrityException("The description has no next current authority.");
        if (next.SystemDescriptionVersionId != previous.SystemDescriptionVersionId)
        {
            if (versions.Any(item => item.VersionId == next.SystemDescriptionVersionId))
                throw new ImmutableContentIntegrityException("A description version identity cannot be reused from historical authority.");
            if (previous.Status == "draft")
                versions[^1] = Token(previous with { Status = "superseded" });
            versions.Add(Token(next));
        }
        else
            versions[^1] = Token(next);
        var currentId = next.SystemDescriptionVersionId;
        return new(current, versions.ToArray(), Append(before.ReviewHashes, mutation is SystemDescriptionReviewedBody review ? review.Review : null),
            Append(before.ApprovalHashes, mutation is SystemDescriptionApprovedBody approval ? approval.Approval : null),
            Append(before.NoChangeDecisionHashes, mutation is SystemDescriptionNoChangeRecordedBody decision ? decision.Decision : null),
            Append(before.BindingHashes, mutation is SystemDescriptionBoundBody binding ? binding.Binding : null),
            mutation is SystemDescriptionApprovedBody approved ? approved.Approval.VersionId : before.LatestApprovedVersionId,
            after.Reviews.Where(item => item.VersionId == currentId).ToArray(), after.Approvals.FirstOrDefault(item => item.VersionId == currentId),
            after.NoChangeDecisions.Where(item => item.VersionId == currentId).ToArray(), after.Bindings.Where(item => item.VersionId == currentId).ToArray());
    }
    public static SystemDescriptionView CurrentView(SystemDescriptionAuthorityHeader h, SystemDescriptionCurrentAuthority authority,
        SystemDescriptionVersionView current) => new(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, current, [current],
            authority.CurrentReviews, authority.CurrentApproval is { } approval ? [approval] : [], authority.CurrentNoChangeDecisions, authority.CurrentBindings);
    public static SystemDescriptionAuthorityHeader Header(SystemDescriptionView view, SystemDescriptionCurrentAuthority authority)
    {
        var current = view.CurrentVersion ?? throw new ImmutableContentIntegrityException("The description has no current version.");
        var review = authority.CurrentReviews.Count == 0 ? null : authority.CurrentReviews[^1];
        return new(view.TenantId, view.ProgramId, view.SystemDescriptionId, view.Revision, current.Version, current.SystemDescriptionVersionId,
            current.ParentVersionId, current.Status, current.Content.Title, current.ContentSha256, current.Scope, current.AuthorMemberId, current.UpdatedAt,
            review?.ReviewId, review?.ReviewerMemberId, review?.Outcome, authority.ReviewHashes.Count, authority.ApprovalHashes.Count,
            authority.NoChangeDecisionHashes.Count, AuthoredIntentDigest.Compute(authority.NoChangeDecisionHashes), authority.BindingHashes.Count,
            AuthoredIntentDigest.Compute(authority.BindingHashes), authority.LatestApprovedVersionId, AuthoredIntentDigest.Compute(authority.Versions),
            AuthoredIntentDigest.Compute(authority.ReviewHashes), AuthoredIntentDigest.Compute(authority.ApprovalHashes));
    }
    public static bool MatchesCurrent(SystemDescriptionAuthorityHeader header, SystemDescriptionCurrentAuthority authority,
        SystemDescriptionVersionView current)
    {
        if (authority.Versions is not { Count: > 0 } || authority.ReviewHashes is null || authority.ApprovalHashes is null ||
            authority.NoChangeDecisionHashes is null || authority.BindingHashes is null || authority.CurrentReviews is null ||
            authority.CurrentNoChangeDecisions is null || authority.CurrentBindings is null ||
            current.TenantId != header.TenantId || current.ProgramId != header.ProgramId || current.SystemDescriptionId != header.SystemDescriptionId ||
            authority.Versions.Select(item => item.VersionId).Distinct().Count() != authority.Versions.Count ||
            authority.Versions.Where((item, index) => item.VersionId == Uuid.Empty || item.Version != index + 1 ||
                item.Status is not ("draft" or "approved" or "superseded") || !Hex(item.Sha256)).Any() ||
            authority.ReviewHashes.Concat(authority.ApprovalHashes).Concat(authority.NoChangeDecisionHashes).Concat(authority.BindingHashes).Any(item => !Hex(item)) ||
            authority.Versions[^1] != Token(current) || !Matches(authority.CurrentVersionContent, current, SystemDescriptionContent.CurrentSchema) ||
            authority.CurrentReviews.Any(item => item.VersionId != current.SystemDescriptionVersionId || !authority.ReviewHashes.Contains(Hash(item))) ||
            authority.CurrentApproval is { } approval && (approval.VersionId != current.SystemDescriptionVersionId || !authority.ApprovalHashes.Contains(Hash(approval))) ||
            authority.CurrentNoChangeDecisions.Any(item => item.VersionId != current.SystemDescriptionVersionId || !authority.NoChangeDecisionHashes.Contains(Hash(item))) ||
            authority.CurrentBindings.Any(item => item.VersionId != current.SystemDescriptionVersionId || !authority.BindingHashes.Contains(Hash(item))))
            return false;
        return Header(CurrentView(header, authority, current), authority) == header;
    }
    public static bool MatchesBefore(SystemDescriptionView view, SystemDescriptionCurrentAuthority authority, bool requireHistory)
    {
        var current = view.CurrentVersion;
        if (current is null || !Matches(authority.CurrentVersionContent, current, SystemDescriptionContent.CurrentSchema))
            return false;
        var expected = FromView(view, authority.CurrentVersionContent);
        return requireHistory ? Equal(expected, authority) :
            Equal(expected.CurrentReviews, authority.CurrentReviews) && Equal(expected.CurrentApproval, authority.CurrentApproval) &&
            Equal(expected.CurrentNoChangeDecisions, authority.CurrentNoChangeDecisions) && Equal(expected.CurrentBindings, authority.CurrentBindings);
    }
    public static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull
    {
        if (reference.Schema != schema || reference.MediaType != ImmutableContentStoreExtensions.JsonMediaType || reference.ContentId == Uuid.Empty)
            return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);
        return reference.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }
    public static bool Equal<T>(T? left, T? right) where T : class => left is null || right is null ? left is null && right is null :
        AuthoredIntentDigest.Compute(left) == AuthoredIntentDigest.Compute(right);
    static IReadOnlyList<string> Append<T>(IReadOnlyList<string> before, T? value) where T : class => value is null ? before : [.. before, Hash(value)];
    static SystemDescriptionVersionToken Token(SystemDescriptionVersionView value) => new(value.SystemDescriptionVersionId, value.Version, value.Status, Hash(value));
    static string Hash<T>(T value) where T : notnull => AuthoredIntentDigest.Compute(value);
    static bool Hex(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
