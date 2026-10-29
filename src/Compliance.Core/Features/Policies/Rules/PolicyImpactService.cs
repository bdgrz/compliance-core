using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
namespace Bdgrz.Compliance.Features.Policies.Rules;

/// <summary>
///     Previews what approving a draft revision changes relative to its predecessor. Campaigns
///     bound to the predecessor are listed because they keep that exact version; a successor
///     never rewrites them.
/// </summary>
public sealed class PolicyImpactService(PolicyReader reader,
    ICampaignDirectoryReader campaigns, IDomainEventReader events)
{
    public async ValueTask<Result<PolicyImpactPreview>> PreviewAsync(Uuid tenantId,
        Uuid programId, Uuid policyId, long expectedRevision, CancellationToken ct)
    {
        var source = await PolicySource.ReadAsync(reader, tenantId, programId, policyId, ct)
            .ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<PolicyImpactPreview>.Failure(source.Error);
        var policy = source.Value;
        if (policy.Revision != expectedRevision)
            return Result<PolicyImpactPreview>.Failure(VersionedRecordRules.StaleRevision(
                "policy draft", policy.Revision).ToRequestError());
        if (policy.DraftContent is not { } draft)
            return Result<PolicyImpactPreview>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The policy has no open draft to preview."));
        var predecessor = policy.DraftPredecessorVersion is { } version
            ? policy.FindVersion(version)
            : null;
        var before = predecessor?.Content;
        var changed = new List<string>();
        void Compare(string field, string? left, string? right)
        {
            if (before is null || !StringComparer.Ordinal.Equals(left, right))
                changed.Add(field);
        }
        Compare("title", before?.Title, draft.Title);
        Compare("purpose", before?.Purpose, draft.Purpose);
        Compare("audience_kind", before?.AudienceKind, draft.AudienceKind);
        Compare("audience_teams", Join(before?.AudienceTeams), Join(draft.AudienceTeams));
        Compare("review_cadence_months", before?.ReviewCadenceMonths.ToString(
            System.Globalization.CultureInfo.InvariantCulture), draft.ReviewCadenceMonths.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Compare("body", before?.Body, draft.Body);
        Compare("source_reference", before?.SourceReference, draft.SourceReference);
        Compare("owner_reference", before?.OwnerReference, draft.OwnerReference);
        var previous = (before?.Applicability ?? []).ToDictionary(Policy.Key, StringComparer.Ordinal);
        var next = (draft.Applicability ?? []).ToDictionary(Policy.Key, StringComparer.Ordinal);
        var added = next.Where(pair => !previous.ContainsKey(pair.Key))
            .Select(static pair => pair.Value).ToArray();
        var removed = previous.Where(pair => !next.ContainsKey(pair.Key))
            .Select(static pair => pair.Value).ToArray();
        var retained = next.Where(pair => previous.ContainsKey(pair.Key))
            .Select(static pair => pair.Value).ToArray();
        if (before is not null && (added.Length > 0 || removed.Length > 0))
            changed.Add("applicability");
        var checkpoint = predecessor is null ? ProjectionCheckpoint.Start :
            await campaigns.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        if (predecessor is not null && !await IsCaughtUpAsync(tenantId, checkpoint, ct)
                .ConfigureAwait(false))
            return BehindSource();
        var affected = predecessor is null
            ? []
            : await campaigns.ListForSubjectAsync(tenantId, programId, policyId,
                predecessor.Version, ct).ConfigureAwait(false);
        if (predecessor is not null &&
            (await campaigns.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false) != checkpoint ||
             !await IsCaughtUpAsync(tenantId, checkpoint, ct).ConfigureAwait(false)))
            return BehindSource();
        var audienceChanged = changed.Contains("audience_kind") ||
                              changed.Contains("audience_teams");
        var digest = new StringBuilder()
            .Append(policyId).Append('|').Append(expectedRevision).Append('|')
            .Append(predecessor?.Version).Append('|').AppendJoin(',', changed).Append('|')
            .AppendJoin(',', added.Select(Policy.Key)).Append('|')
            .AppendJoin(',', removed.Select(Policy.Key)).Append('|')
            .AppendJoin(',', affected.Order()).Append('|').Append(audienceChanged)
            .ToString();
        return Result<PolicyImpactPreview>.Success(new PolicyImpactPreview(tenantId, programId,
            policyId, expectedRevision, predecessor?.Version, changed, added, removed, retained,
            affected, audienceChanged, Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(digest)))));
    }

    static string Join(IReadOnlyList<string>? values) => string.Join('\n', values ?? []);

    async ValueTask<bool> IsCaughtUpAsync(Uuid tenantId, ProjectionCheckpoint checkpoint,
        CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                tenantId.ToString(), "policy-distribution-campaigns"), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return !await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static Result<PolicyImpactPreview> BehindSource() => Result<PolicyImpactPreview>.Failure(
        new RequestError(RequestErrorKind.Conflict,
            "The policy campaign projection changed or has not reached the source. Reload the impact preview.",
            isTransient: true));
}
