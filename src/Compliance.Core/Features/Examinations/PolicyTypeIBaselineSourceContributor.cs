using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures policy versions and lifecycle decisions known on the Type I date.</summary>
public sealed class PolicyTypeIBaselineSourceContributor(IPolicyDirectoryReader directory,
    IAggregateReader reader, PolicyDirectoryReadConsistency consistency)
    : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;

    public string Family => "policies";

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct)
    {
        var fence = await consistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(fence.Error);

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var sources = new List<TypeIBaselineSourceReference>();
        var scopeDifferences = new List<string>();
        var seenPolicyIds = new HashSet<Uuid>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(engagement.TenantId,
                engagement.ProgramId, PageSize, cursor, ct).ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory();

            foreach (var item in page.Items)
            {
                if (item is null || item.TenantId != engagement.TenantId ||
                    item.ProgramId != engagement.ProgramId || item.PolicyId == Uuid.Empty ||
                    item.Revision < 1 || string.IsNullOrWhiteSpace(item.Identifier) ||
                    Policy.NormalizeIdentifier(item.Identifier) != item.Identifier ||
                    !seenPolicyIds.Add(item.PolicyId))
                    return InvalidDirectory();

                var policy = await reader.HydrateAsync(new Policy(engagement.TenantId,
                    item.PolicyId), ct).ConfigureAwait(false);
                if (!policy.IsVisible || policy.Id != item.PolicyId ||
                    policy.ProgramId != engagement.ProgramId ||
                    policy.Identifier != item.Identifier || policy.Revision != item.Revision)
                    return SourceConflict();

                var versions = policy.ReadVersions();
                if (versions.Count == 0)
                    continue;
                var decisions = policy.ReadDecisions();
                if (!IsValidHistory(engagement.TenantId, policy, versions, decisions))
                    return SourceConflict();

                var eligibleVersions = versions.Where(version => version.ApprovedAt <= through &&
                    version.EffectiveFrom <= engagement.PeriodStart).ToArray();
                var effective = eligibleVersions.LastOrDefault();
                var retirement = effective is null ? null : decisions.SingleOrDefault(decision =>
                    decision.Kind == "retirement" && decision.Version == effective.Version &&
                    decision.DecidedAt <= through);

                if (retirement is not null)
                {
                    if (effective!.EffectiveUntil is not { } effectiveUntil)
                        return SourceConflict();
                    try
                    {
                        sources.Add(RetirementReference(policy.Id, effective, retirement,
                            effectiveUntil));
                    }
                    catch (Exception exception) when (exception is ArgumentException or
                        JsonException or InvalidOperationException or NotSupportedException)
                    {
                        scopeDifferences.Add($"Policy {item.Identifier} has invalid canonical retirement content.");
                    }

                    if (effectiveUntil <= engagement.PeriodStart)
                    {
                        scopeDifferences.Add(
                            $"Policy {item.Identifier} was retired on or before the Type I date.");
                        continue;
                    }
                    scopeDifferences.Add(
                        $"Policy {item.Identifier} has a retirement scheduled after the Type I date.");
                }

                if (effective is null)
                {
                    if (versions.Any(version => version.ApprovedAt <= through))
                        scopeDifferences.Add(
                            $"Policy {item.Identifier} has no approved version effective on the Type I date.");
                    else
                        scopeDifferences.Add(
                            $"Policy {item.Identifier} has no version approved by the Type I date.");
                    continue;
                }

                if (retirement is not null && effective.EffectiveUntil <= engagement.PeriodStart)
                    continue;

                var periodicReviews = decisions.Where(decision =>
                        decision.Kind == "periodic_review" && decision.Version == effective.Version &&
                        decision.DecidedAt <= through)
                    .OrderBy(static decision => decision.DecidedAt)
                    .ThenBy(static decision => decision.DecisionId.ToString(), StringComparer.Ordinal)
                    .ToArray();
                try
                {
                    sources.Add(VersionReference(policy.Id, effective, periodicReviews,
                        decisions.Single(decision =>
                            decision.DecisionId == effective.ApprovalDecisionId),
                        decisions.Single(decision =>
                            decision.DecisionId == effective.AcceptedReviewDecisionId)));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                    InvalidOperationException or NotSupportedException)
                {
                    scopeDifferences.Add($"Policy {item.Identifier} has invalid canonical source content.");
                    continue;
                }

                var lastReviewed = periodicReviews.LastOrDefault()?.DecidedAt ?? effective.ApprovedAt;
                var dueOn = DateOnly.FromDateTime(lastReviewed.UtcDateTime)
                    .AddMonths(effective.Content.ReviewCadenceMonths);
                if (engagement.PeriodStart > dueOn)
                    scopeDifferences.Add(
                        $"Policy {item.Identifier} was overdue for periodic review on the Type I date.");
            }

            var next = page.NextCursor;
            if (next is not null && (next == cursor || !seenCursors.Add(next)))
                return InvalidDirectory();
            cursor = next;
        } while (cursor is not null);

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmed.Error);

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(), [],
                scopeDifferences.AsReadOnly()));
    }

    static bool IsValidHistory(Uuid tenantId, Policy policy,
        IReadOnlyList<PolicyVersionView> versions,
        IReadOnlyList<PolicyDecisionView> decisions)
    {
        var seenDecisionIds = new HashSet<Uuid>();
        for (var index = 0; index < decisions.Count; index++)
        {
            var decision = decisions[index];
            if (decision is null || decision.TenantId != tenantId ||
                decision.ProgramId != policy.ProgramId || decision.PolicyId != policy.Id ||
                decision.DecisionId == Uuid.Empty || !seenDecisionIds.Add(decision.DecisionId) ||
                string.IsNullOrWhiteSpace(decision.Rationale) || decision.DecidedAt == default ||
                decision.Actor is null || decision.Kind is not
                    ("review" or "approval" or "periodic_review" or "retirement"))
                return false;
            if (decision.Kind == "review" &&
                (decision.Outcome is not ("accept" or "request_changes") ||
                 decision.Revision is not { } reviewRevision || reviewRevision < 1 ||
                 reviewRevision > policy.Revision || decision.Version is not null))
                return false;
        }

        for (var index = 0; index < versions.Count; index++)
        {
            var version = versions[index];
            var expectedVersion = index + 1L;
            long? expectedPredecessor = index == 0 ? null : expectedVersion - 1;
            if (version is null || version.TenantId != tenantId ||
                version.ProgramId != policy.ProgramId || version.PolicyId != policy.Id ||
                version.Identifier != policy.Identifier || version.Version != expectedVersion ||
                version.Revision < 1 || version.Revision > policy.Revision ||
                version.PredecessorVersion != expectedPredecessor || version.EffectiveFrom == default ||
                version.Status is not ("approved" or "superseded" or "retired") ||
                version.Content is null || version.ContentSha256 != Policy.Hash(version.Content) ||
                version.ApprovalDecisionId == Uuid.Empty ||
                version.AcceptedReviewDecisionId == Uuid.Empty ||
                version.ApprovalDecisionId == version.AcceptedReviewDecisionId ||
                version.ApprovedBy is null || version.ApprovedAt == default ||
                index > 0 && (version.EffectiveFrom <= versions[index - 1].EffectiveFrom ||
                              version.Revision <= versions[index - 1].Revision))
                return false;

            var approvals = decisions.Where(decision =>
                decision.DecisionId == version.ApprovalDecisionId).ToArray();
            var reviews = decisions.Where(decision =>
                decision.DecisionId == version.AcceptedReviewDecisionId).ToArray();
            if (approvals.Length != 1 || reviews.Length != 1 ||
                !MatchesApproval(approvals[0], version) || !MatchesAcceptedReview(reviews[0], version))
                return false;
        }

        foreach (var decision in decisions)
        {
            if (decision.Kind == "periodic_review" &&
                (decision.Outcome != "confirmed" || decision.Revision is not null ||
                 decision.Version is not { } reviewedVersion ||
                 reviewedVersion > versions.Count || reviewedVersion < 1))
                return false;
            if (decision.Kind == "retirement" &&
                (decision.Outcome != "retire" || decision.Version is not { } retiredVersion ||
                 retiredVersion > versions.Count || retiredVersion < 1 ||
                 decision.Revision is not { } retirementRevision ||
                 retirementRevision < versions[(int)retiredVersion - 1].Revision))
                return false;
        }

        if (decisions.Count(static decision => decision.Kind == "approval") != versions.Count)
            return false;
        var retirements = decisions.Where(static decision => decision.Kind == "retirement").ToArray();
        if (retirements.Length > 1 || retirements.Length == 1 &&
            retirements[0].Version != versions.Count)
            return false;
        for (var index = 0; index < versions.Count; index++)
        {
            var expectedStatus = index < versions.Count - 1 ? "superseded" :
                retirements.Length == 1 ? "retired" : "approved";
            var version = versions[index];
            if (version.Status != expectedStatus ||
                version.Status == "retired" && (version.EffectiveUntil is not { } until ||
                    until <= version.EffectiveFrom) ||
                version.Status == "superseded" && version.EffectiveUntil !=
                    versions[index + 1].EffectiveFrom ||
                version.Status == "approved" && version.EffectiveUntil is not null)
                return false;
        }
        return true;
    }

    static bool MatchesApproval(PolicyDecisionView decision, PolicyVersionView version) =>
        decision.Kind == "approval" && decision.Outcome == "approve" &&
        decision.Revision == version.Revision && decision.Version == version.Version &&
        decision.Actor == version.ApprovedBy && decision.DecidedAt == version.ApprovedAt &&
        decision.SeparationOfDutiesWaiverId == version.SeparationOfDutiesWaiverId;

    static bool MatchesAcceptedReview(PolicyDecisionView decision, PolicyVersionView version) =>
        decision.Kind == "review" && decision.Outcome == "accept" &&
        decision.Revision == version.Revision && decision.DecidedAt <= version.ApprovedAt;

    static TypeIBaselineSourceReference VersionReference(Uuid policyId,
        PolicyVersionView version, IReadOnlyList<PolicyDecisionView> periodicReviews,
        PolicyDecisionView approvalDecision, PolicyDecisionView acceptedReviewDecision)
    {
        var content = new PolicyVersionSourceContent(version.TenantId, version.ProgramId,
            version.PolicyId, version.Identifier, version.Version, version.Revision, version.Major,
            version.Content, version.ContentSha256, version.EffectiveFrom,
            version.PredecessorVersion, version.ApprovalDecisionId,
            version.AcceptedReviewDecisionId, version.ApprovedBy, version.ApprovedAt,
            version.ImpactDigest, version.SeparationOfDutiesWaiverId, approvalDecision,
            acceptedReviewDecision, periodicReviews);
        return new TypeIBaselineSourceReference("policy_version", policyId,
            Uuid.CreateVersion5(policyId, $"policy_version_v1:{version.Version}"),
            version.Revision, Digest("bdgrz.snapshot.source.policy_version.v1\0", content,
                ComplianceCoreJsonContext.Default.PolicyVersionSourceContent));
    }

    static TypeIBaselineSourceReference RetirementReference(Uuid policyId,
        PolicyVersionView version, PolicyDecisionView retirement, DateOnly effectiveUntil)
    {
        var content = new PolicyRetirementSourceContent(retirement.TenantId,
            retirement.ProgramId, retirement.PolicyId, retirement.DecisionId,
            retirement.Revision!.Value, version.Version, retirement.Outcome,
            retirement.Rationale, retirement.Actor, retirement.DecidedAt,
            retirement.SeparationOfDutiesWaiverId, effectiveUntil);
        return new TypeIBaselineSourceReference("policy_retirement", policyId,
            Uuid.CreateVersion5(policyId, $"policy_retirement_v1:{retirement.Revision}"),
            retirement.Revision.Value, Digest("bdgrz.snapshot.source.policy_retirement.v1\0",
                content, ComplianceCoreJsonContext.Default.PolicyRetirementSourceContent));
    }

    static string Digest<T>(string domain, T content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(content, typeInfo);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
            WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(domain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name.Normalize(NormalizationForm.FormC));
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString()!.Normalize(NormalizationForm.FormC));
                break;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var number))
                    throw new InvalidOperationException(
                        "Policy baseline numbers must be signed 64-bit integers.");
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException(
                    "Unsupported content entered a policy baseline snapshot.");
        }
    }

    static RequestError InvalidDirectoryError() => new(RequestErrorKind.Conflict,
        "The policy directory returned an invalid or unstable program page.");

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(InvalidDirectoryError());

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The policy source changed or did not match its program directory. Retry the preview.",
            isTransient: true));
}
