using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures approved access expectations and their exceptions on the Type I date.</summary>
public sealed class AccessExpectationsTypeIBaselineSourceContributor(
    IAggregateReader reader, IDomainEventReader events) : ITypeIBaselineSourceContributor
{
    const string DigestDomain = "bdgrz.snapshot.source.access_expectation_ledger.v1\0";

    public string Family => "access_expectations";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null ||
            engagement.TenantId == Uuid.Empty || engagement.ProgramId == Uuid.Empty ||
            engagement.PeriodStart == default)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "An access expectation baseline read requires its engagement and source context."));

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var systemIds = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        foreach (var source in context.PreviouslyCapturedSources.Where(static source =>
                     source.Kind == "reviewed_system_revision"))
        {
            if (source.RecordId == Uuid.Empty || source.Revision < 1 ||
                source.VersionId != Uuid.CreateVersion5(source.RecordId,
                    $"reviewed_system_revision_v1:{source.Revision}") ||
                !IsSha256(source.ContentSha256) || !systemIds.Add(source.RecordId))
                return SourceConflict();
        }

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var systemId in systemIds)
        {
            var stream = new EventStreamAddress(engagement.TenantId.ToString(),
                AccessReviewSystemLedger.Area, systemId.ToString());
            var history = await ReadHistoryAsync(stream, engagement.TenantId, systemId,
                through, ct).ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (history.Value.AsOfRevision == 0)
            {
                scopeDifferences.Add(
                    $"Reviewed system {systemId} has no access expectation ledger recorded on the Type I date.");
                continue;
            }

            if (!HasEffectiveExpectation(history.Value.Expectations, through))
                scopeDifferences.Add(
                    $"Reviewed system {systemId} has no approved access expectation effective on the Type I date.");

            try
            {
                var content = new AccessExpectationLedgerSourceContent(engagement.PeriodStart,
                    systemId, history.Value.Expectations, history.Value.Exceptions,
                    history.Value.PopulationExceptions);
                sources.Add(new TypeIBaselineSourceReference("access_expectation_ledger",
                    systemId, Uuid.CreateVersion5(systemId,
                        "access_expectation_ledger_v1:" + engagement.PeriodStart.ToString(
                            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                    history.Value.AsOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                blockers.Add(
                    $"Reviewed system {systemId} has invalid canonical access expectation content.");
                scopeDifferences.Add(
                    $"Reviewed system {systemId} has invalid canonical access expectation content.");
            }
        }

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(), scopeDifferences.AsReadOnly()));
    }

    async ValueTask<Result<AccessExpectationHistory>> ReadHistoryAsync(EventStreamAddress stream,
        Uuid tenantId, Uuid systemId, DateTimeOffset through, CancellationToken ct)
    {
        var currentExpectations = new List<AccessExpectationView>();
        var currentExceptionIds = new HashSet<Uuid>();
        var currentExceptions = new List<AccessExpectationExceptionView>();
        var currentPopulationExceptionIds = new HashSet<Uuid>();
        var currentPopulationExceptions = new List<AccessPopulationExceptionView>();
        var expectationIndexes = new Dictionary<Uuid, int>();
        var asOfExpectations = new List<AccessExpectationView>();
        var asOfExpectationIndexes = new Dictionary<Uuid, int>();
        var asOfExceptions = new List<AccessExpectationExceptionView>();
        var asOfPopulationExceptions = new List<AccessPopulationExceptionView>();
        var seenEventIds = new HashSet<Uuid>();
        ulong expectedVersion = 1;
        long expectedRevision = 1;
        long asOfRevision = 0;
        DateTimeOffset? priorOccurredAt = null;
        DateTimeOffset? priorRecordedAt = null;

        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var domainEvent = record.Event;
            var metadata = domainEvent?.Metadata;
            if (record.Stream != stream || domainEvent is null || metadata is null ||
                metadata.EventId == Uuid.Empty || !seenEventIds.Add(metadata.EventId) ||
                metadata.AggregateId != systemId || metadata.AggregateVersion != expectedVersion ||
                metadata.OccurredOn == default ||
                priorRecordedAt is { } earlierRecord && metadata.OccurredOn < earlierRecord)
                return InvalidHistory();

            DateTimeOffset occurredAt;
            switch (domainEvent)
            {
                case AccessExpectationProposed proposed:
                    var expectation = proposed.Expectation;
                    if (proposed.TenantId != tenantId || proposed.SystemInstanceId != systemId ||
                        proposed.Revision != expectedRevision || proposed.ProposerMemberId == Uuid.Empty ||
                        !ValidProposedExpectation(expectation, systemId) ||
                        !ValidMemberActor(expectation.ProposedBy, proposed.ProposerMemberId) ||
                        expectationIndexes.ContainsKey(expectation.ExpectationId) ||
                        expectation.SupersedesExpectationId is { } predecessorId &&
                        (!expectationIndexes.TryGetValue(predecessorId, out var predecessorIndex) ||
                         currentExpectations[predecessorIndex].Status != AccessReviewSystemLedger.Approved))
                        return InvalidHistory();

                    occurredAt = expectation.ProposedAt;
                    expectationIndexes.Add(expectation.ExpectationId, currentExpectations.Count);
                    currentExpectations.Add(expectation);
                    if (occurredAt <= through && metadata.OccurredOn <= through)
                    {
                        asOfExpectationIndexes.Add(expectation.ExpectationId, asOfExpectations.Count);
                        asOfExpectations.Add(expectation);
                    }
                    break;

                case AccessExpectationApproved approved:
                    if (approved.TenantId != tenantId || approved.SystemInstanceId != systemId ||
                        approved.Revision != expectedRevision ||
                        !expectationIndexes.TryGetValue(approved.ExpectationId, out var index) ||
                        currentExpectations[index] is not { Status: AccessReviewSystemLedger.Proposed } proposedExpectation ||
                        approved.ApprovedAt < proposedExpectation.ProposedAt || approved.ApprovedAt == default ||
                        !ValidMemberActor(approved.ApprovedBy) ||
                        approved.SeparationOfDutiesWaiverId == Uuid.Empty)
                        return InvalidHistory();

                    occurredAt = approved.ApprovedAt;
                    var approvedExpectation = proposedExpectation with
                    {
                        Status = AccessReviewSystemLedger.Approved,
                        ApprovedBy = approved.ApprovedBy,
                        ApprovedAt = approved.ApprovedAt,
                        SeparationOfDutiesWaiverId = approved.SeparationOfDutiesWaiverId,
                    };
                    currentExpectations[index] = approvedExpectation;
                    if (occurredAt <= through && metadata.OccurredOn <= through)
                    {
                        if (!asOfExpectationIndexes.TryGetValue(approved.ExpectationId,
                                out var asOfIndex))
                            return InvalidHistory();
                        asOfExpectations[asOfIndex] = approvedExpectation;
                    }
                    break;

                case AccessExpectationExceptionRecorded exceptionRecorded:
                    var expectationException = exceptionRecorded.Exception;
                    if (exceptionRecorded.TenantId != tenantId ||
                        exceptionRecorded.SystemInstanceId != systemId ||
                        exceptionRecorded.Revision != expectedRevision ||
                        !ValidExpectationException(expectationException) ||
                        !currentExceptionIds.Add(expectationException.ExceptionId) ||
                        !expectationIndexes.TryGetValue(expectationException.ExpectationId,
                            out var exceptionExpectationIndex))
                        return InvalidHistory();
                    var exceptionExpectation = currentExpectations[exceptionExpectationIndex];
                    if (exceptionExpectation.Status != AccessReviewSystemLedger.Approved ||
                        exceptionExpectation.ApprovedAt > expectationException.ApprovedAt)
                        return InvalidHistory();
                    occurredAt = expectationException.ApprovedAt;
                    currentExceptions.Add(expectationException);
                    if (occurredAt <= through && metadata.OccurredOn <= through)
                    {
                        if (!asOfExpectationIndexes.ContainsKey(expectationException.ExpectationId))
                            return InvalidHistory();
                        asOfExceptions.Add(expectationException);
                    }
                    break;

                case AccessPopulationExceptionRecorded populationExceptionRecorded:
                    var populationException = populationExceptionRecorded.Exception;
                    if (populationExceptionRecorded.TenantId != tenantId ||
                        populationExceptionRecorded.SystemInstanceId != systemId ||
                        populationExceptionRecorded.Revision != expectedRevision ||
                        !ValidPopulationException(populationException, systemId) ||
                        !currentPopulationExceptionIds.Add(populationException.ExceptionId))
                        return InvalidHistory();
                    occurredAt = populationException.ApprovedAt;
                    currentPopulationExceptions.Add(populationException);
                    if (occurredAt <= through && metadata.OccurredOn <= through)
                        asOfPopulationExceptions.Add(populationException);
                    break;

                default:
                    return InvalidHistory();
            }

            if (priorOccurredAt is { } earlierOccurredAt && occurredAt < earlierOccurredAt)
                return InvalidHistory();
            priorOccurredAt = occurredAt;
            priorRecordedAt = metadata.OccurredOn;
            if (occurredAt <= through && metadata.OccurredOn <= through)
                asOfRevision = expectedRevision;
            expectedRevision++;
            expectedVersion++;
        }

        var ledger = await reader.HydrateAsync(new AccessReviewSystemLedger(tenantId, systemId), ct)
            .ConfigureAwait(false);
        var view = ledger.ToView();
        if (ledger.Id != systemId || ledger.Revision != expectedRevision - 1 ||
            view.TenantId != tenantId || view.SystemInstanceId != systemId ||
            view.Revision != expectedRevision - 1 ||
            !view.Expectations.SequenceEqual(currentExpectations) ||
            !view.Exceptions.SequenceEqual(currentExceptions) ||
            !view.PopulationExceptions.SequenceEqual(currentPopulationExceptions))
            return InvalidHistory();

        return Result<AccessExpectationHistory>.Success(new AccessExpectationHistory(asOfRevision,
            asOfExpectations.AsReadOnly(), asOfExceptions.AsReadOnly(),
            asOfPopulationExceptions.AsReadOnly()));
    }

    static bool ValidProposedExpectation(AccessExpectationView? expectation, Uuid systemId) =>
        expectation is not null && expectation.ExpectationId != Uuid.Empty &&
        expectation.SystemInstanceId == systemId &&
        expectation.Status == AccessReviewSystemLedger.Proposed &&
        expectation.ProposedBy is not null && expectation.ProposedAt != default &&
        expectation.ApprovedBy is null && expectation.ApprovedAt is null &&
        expectation.SeparationOfDutiesWaiverId is null &&
        expectation.EffectiveFrom != default &&
        (expectation.EffectiveUntil is null || expectation.EffectiveUntil > expectation.EffectiveFrom) &&
        AccessReviewVocabulary.IsBoundedText(expectation.Rationale, 4000) &&
        expectation.Rationale == expectation.Rationale.Trim() &&
        ValidRule(expectation.RuleKind, expectation.Parameters);

    static bool ValidRule(string ruleKind, AccessExpectationParameters? parameters)
    {
        if (parameters is null || !AccessReviewVocabulary.RuleKinds.Contains(ruleKind) ||
            parameters.PrincipalKind is { } principalKind &&
            !AccessReviewVocabulary.PrincipalKinds.Contains(principalKind) ||
            parameters.EntitlementKind is { } entitlementKind &&
            !AccessReviewVocabulary.EntitlementKinds.Contains(entitlementKind) ||
            parameters.ProviderEntitlementId is { Length: 0 or > 512 } ||
            parameters.ProviderSubjectId is { Length: 0 or > 512 })
            return false;

        var entitlementMatch = parameters.EntitlementKind is not null ||
                               parameters.ProviderEntitlementId is not null;
        return ruleKind switch
        {
            AccessReviewVocabulary.ForbiddenPrincipalKind =>
                parameters.PrincipalKind is not null && !entitlementMatch &&
                parameters.ProviderSubjectId is null,
            AccessReviewVocabulary.PrivilegedEntitlement or AccessReviewVocabulary.RequiresExpiry =>
                entitlementMatch && parameters.ProviderSubjectId is null,
            AccessReviewVocabulary.RequiredAccess =>
                parameters.ProviderSubjectId is not null &&
                parameters.ProviderEntitlementId is not null &&
                parameters.EntitlementKind is null && parameters.PrincipalKind is null,
            _ => false,
        };
    }

    static bool ValidExpectationException(AccessExpectationExceptionView? exception) =>
        exception is not null && exception.ExceptionId != Uuid.Empty &&
        exception.ExpectationId != Uuid.Empty &&
        AccessReviewVocabulary.IsBoundedText(exception.ProviderSubjectId, 512) &&
        exception.ProviderEntitlementId is not { Length: 0 or > 512 } &&
        AccessReviewVocabulary.IsBoundedText(exception.Rationale, 4000) &&
        exception.Rationale == exception.Rationale.Trim() &&
        exception.ExpiresAt > exception.ApprovedAt && exception.ApprovedAt != default &&
        ValidMemberActor(exception.ApprovedBy);

    static bool ValidPopulationException(AccessPopulationExceptionView? exception,
        Uuid systemId) => exception is not null && exception.ExceptionId != Uuid.Empty &&
        exception.SystemInstanceId == systemId &&
        AccessReviewVocabulary.IsBoundedText(exception.Reason, 4000) &&
        exception.Reason == exception.Reason.Trim() && exception.ExpiresAt > exception.ApprovedAt &&
        exception.ApprovedAt != default && ValidMemberActor(exception.ApprovedBy);

    static bool ValidMemberActor(ActorReference? actor) => actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, null, out var memberId) && memberId != Uuid.Empty &&
        AccessReviewVocabulary.IsBoundedText(actor.Display, 200) &&
        actor.Display == actor.Display.Trim();

    static bool ValidMemberActor(ActorReference? actor, Uuid memberId) =>
        memberId != Uuid.Empty && ValidMemberActor(actor) && actor!.Id == memberId.ToString();

    static bool HasEffectiveExpectation(IReadOnlyList<AccessExpectationView> expectations,
        DateTimeOffset at) => expectations.Any(expectation =>
        expectation.Status == AccessReviewSystemLedger.Approved &&
        expectation.EffectiveFrom <= at &&
        (expectation.EffectiveUntil is null || at < expectation.EffectiveUntil) &&
        !expectations.Any(successor => successor.Status == AccessReviewSystemLedger.Approved &&
            successor.SupersedesExpectationId == expectation.ExpectationId &&
            successor.EffectiveFrom <= at));

    static string Digest(AccessExpectationLedgerSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.AccessExpectationLedgerSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<AccessExpectationHistory> InvalidHistory() =>
        Result<AccessExpectationHistory>.Failure(new RequestError(RequestErrorKind.Conflict,
            "An access expectation ledger is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "A reviewed-system reference is invalid or does not match its access expectation ledger."));

    sealed record AccessExpectationHistory(long AsOfRevision,
        IReadOnlyList<AccessExpectationView> Expectations,
        IReadOnlyList<AccessExpectationExceptionView> Exceptions,
        IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions);
}
