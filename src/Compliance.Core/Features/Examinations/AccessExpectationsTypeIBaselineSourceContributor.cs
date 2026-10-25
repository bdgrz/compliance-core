using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Snapshots;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures approved access expectations and their exceptions on the Type I date.</summary>
public sealed class AccessExpectationsTypeIBaselineSourceContributor(
    IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
    : ITypeIBaselineSourceContributor
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

        var effectiveThrough = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        var through = context.RecordedThrough ?? effectiveThrough;
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
            var history = await ReadHistoryAsync(context, engagement.TenantId, systemId, through, ct)
                .ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (history.Value.AsOfRevision == 0)
            {
                scopeDifferences.Add(
                    $"Reviewed system {systemId} has no access expectation ledger recorded on the Type I date.");
                continue;
            }

            if (!HasEffectiveExpectation(history.Value.Expectations, effectiveThrough))
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

    async ValueTask<Result<AccessExpectationHistory>> ReadHistoryAsync(TypeIBaselineSourceReadContext context, Uuid tenantId, Uuid systemId,
        DateTimeOffset through, CancellationToken ct)
    {
        var lines = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           AccessExpectation.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is AccessExpectationProposed proposed &&
                proposed.SystemInstanceId == systemId)
                lines.Add(Uuid.Parse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture));
        var exceptionStreams = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           AccessPopulationException.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is AccessPopulationExceptionRecorded recorded &&
                recorded.SystemInstanceId == systemId)
                exceptionStreams.Add(Uuid.Parse(record.Stream.Resource,
                    System.Globalization.CultureInfo.InvariantCulture));

        var asOfExpectations = new List<AccessExpectationView>();
        var asOfExceptions = new List<AccessExpectationExceptionView>();
        var asOfPopulationExceptions = new List<AccessPopulationExceptionView>();
        long asOfFacts = 0;
        foreach (var lineId in lines)
        {
            var line = await ReadLineAsync(context, tenantId, systemId, lineId, through, ct).ConfigureAwait(false);
            if (!line.IsSuccess)
                return Result<AccessExpectationHistory>.Failure(line.Error);
            asOfExpectations.AddRange(line.Value.Expectations);
            asOfExceptions.AddRange(line.Value.Exceptions);
            asOfFacts += line.Value.Facts;
        }

        foreach (var exceptionId in exceptionStreams)
        {
            var exception = await ReadPopulationExceptionAsync(context, tenantId, systemId, exceptionId, through, ct)
                .ConfigureAwait(false);
            if (!exception.IsSuccess)
                return Result<AccessExpectationHistory>.Failure(exception.Error);
            if (exception.Value is { } included)
            {
                asOfPopulationExceptions.Add(included);
                asOfFacts++;
            }
        }

        return Result<AccessExpectationHistory>.Success(new AccessExpectationHistory(asOfFacts,
            asOfExpectations.OrderBy(static item => item.ProposedAt)
                .ThenBy(static item => item.ExpectationId.ToString(), StringComparer.Ordinal).ToArray(),
            asOfExceptions.OrderBy(static item => item.ApprovedAt)
                .ThenBy(static item => item.ExceptionId.ToString(), StringComparer.Ordinal).ToArray(),
            asOfPopulationExceptions.OrderBy(static item => item.ApprovedAt)
                .ThenBy(static item => item.ExceptionId.ToString(), StringComparer.Ordinal).ToArray()));
    }

    async ValueTask<Result<LineHistory>> ReadLineAsync(TypeIBaselineSourceReadContext context, Uuid tenantId, Uuid systemId, Uuid lineId,
        DateTimeOffset through, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), AccessExpectation.Area, lineId.ToString());
        var currentExpectations = new List<AccessExpectationView>();
        var currentExceptionIds = new HashSet<Uuid>();
        var expectationIndexes = new Dictionary<Uuid, int>();
        var asOfExpectations = new List<AccessExpectationView>();
        var asOfExpectationIndexes = new Dictionary<Uuid, int>();
        var asOfExceptions = new List<AccessExpectationExceptionView>();
        var seenEventIds = new HashSet<Uuid>();
        ulong expectedVersion = 1;
        long expectedRevision = 1;
        long asOfFacts = 0;
        DateTimeOffset? priorOccurredAt = null;
        DateTimeOffset? priorRecordedAt = null;

        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var domainEvent = record.Event;
            var metadata = domainEvent?.Metadata;
            if (record.Stream != stream || domainEvent is null || metadata is null ||
                metadata.EventId == Uuid.Empty || !seenEventIds.Add(metadata.EventId) ||
                metadata.AggregateId != lineId || metadata.AggregateVersion != expectedVersion ||
                metadata.OccurredOn == default ||
                context.RecordedThrough is null && priorRecordedAt is { } earlierRecord && metadata.OccurredOn < earlierRecord)
                return InvalidLine();

            DateTimeOffset occurredAt;
            switch (domainEvent)
            {
                case AccessExpectationProposed proposed:
                    var expectation = await ReadContentAsync<AccessExpectationView>(tenantId,
                        proposed.ExpectationContent, ct).ConfigureAwait(false);
                    if (proposed.TenantId != tenantId || proposed.SystemInstanceId != systemId ||
                        proposed.Revision != expectedRevision || proposed.ProposerMemberId == Uuid.Empty ||
                        !ValidProposedExpectation(expectation, systemId) ||
                        expectation!.ExpectationId != proposed.Operation.LogicalId ||
                        expectation.Revision != proposed.Revision ||
                        !ValidMemberActor(expectation.ProposedBy, proposed.ProposerMemberId) ||
                        expectationIndexes.ContainsKey(expectation.ExpectationId) ||
                        expectation.SupersedesExpectationId is { } predecessorId &&
                        (!expectationIndexes.TryGetValue(predecessorId, out var predecessorIndex) ||
                         currentExpectations[predecessorIndex].Status != AccessExpectation.Approved))
                        return InvalidLine();

                    occurredAt = expectation.ProposedAt;
                    expectationIndexes.Add(expectation.ExpectationId, currentExpectations.Count);
                    currentExpectations.Add(expectation);
                    if (context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                        await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false))
                    {
                        asOfExpectationIndexes.Add(expectation.ExpectationId, asOfExpectations.Count);
                        asOfExpectations.Add(expectation);
                    }
                    break;

                case AccessExpectationApproved approved:
                    if (approved.TenantId != tenantId || approved.SystemInstanceId != systemId ||
                        approved.Revision != expectedRevision ||
                        !expectationIndexes.TryGetValue(approved.ExpectationId, out var index) ||
                        currentExpectations[index] is not { Status: AccessExpectation.Proposed } proposedExpectation ||
                        approved.ApprovedAt < proposedExpectation.ProposedAt || approved.ApprovedAt == default ||
                        !ValidMemberActor(approved.ApprovedBy) ||
                        approved.SeparationOfDutiesWaiverId == Uuid.Empty)
                        return InvalidLine();

                    occurredAt = approved.ApprovedAt;
                    var approvedExpectation = proposedExpectation with
                    {
                        Status = AccessExpectation.Approved,
                        ApprovedBy = approved.ApprovedBy,
                        ApprovedAt = approved.ApprovedAt,
                        SeparationOfDutiesWaiverId = approved.SeparationOfDutiesWaiverId,
                    };
                    currentExpectations[index] = approvedExpectation;
                    if (context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                        await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false))
                    {
                        if (!asOfExpectationIndexes.TryGetValue(approved.ExpectationId, out var asOfIndex))
                            return InvalidLine();
                        asOfExpectations[asOfIndex] = approvedExpectation;
                    }
                    break;

                case AccessExpectationExceptionRecorded exceptionRecorded:
                    var expectationException = await ReadContentAsync<AccessExpectationExceptionView>(tenantId,
                        exceptionRecorded.ExceptionContent, ct).ConfigureAwait(false);
                    if (exceptionRecorded.TenantId != tenantId ||
                        exceptionRecorded.SystemInstanceId != systemId ||
                        exceptionRecorded.Revision != expectedRevision ||
                        !ValidExpectationException(expectationException) ||
                        expectationException!.ExceptionId != exceptionRecorded.Operation.LogicalId ||
                        !currentExceptionIds.Add(expectationException.ExceptionId) ||
                        !expectationIndexes.TryGetValue(expectationException.ExpectationId,
                            out var exceptionExpectationIndex))
                        return InvalidLine();
                    var exceptionExpectation = currentExpectations[exceptionExpectationIndex];
                    if (exceptionExpectation.Status != AccessExpectation.Approved ||
                        exceptionExpectation.ApprovedAt > expectationException.ApprovedAt)
                        return InvalidLine();
                    occurredAt = expectationException.ApprovedAt;
                    if (context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                        await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false))
                    {
                        if (!asOfExpectationIndexes.ContainsKey(expectationException.ExpectationId))
                            return InvalidLine();
                        asOfExceptions.Add(expectationException);
                    }
                    break;

                default:
                    return InvalidLine();
            }

            if (context.RecordedThrough is null && priorOccurredAt is { } earlierOccurredAt && occurredAt < earlierOccurredAt)
                return InvalidLine();
            priorOccurredAt = occurredAt;
            priorRecordedAt = metadata.OccurredOn;
            if (context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                        await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false))
                asOfFacts++;
            expectedRevision++;
            expectedVersion++;
        }

        var owner = await reader.HydrateAsync(new AccessExpectation(tenantId, lineId), ct).ConfigureAwait(false);
        if (owner.Id != lineId || owner.Revision != expectedRevision - 1 || owner.SystemInstanceId != systemId)
            return InvalidLine();
        return Result<LineHistory>.Success(new LineHistory(asOfFacts, asOfExpectations, asOfExceptions));
    }

    async ValueTask<Result<AccessPopulationExceptionView?>> ReadPopulationExceptionAsync(TypeIBaselineSourceReadContext context, Uuid tenantId,
        Uuid systemId, Uuid exceptionId, DateTimeOffset through, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), AccessPopulationException.Area,
            exceptionId.ToString());
        AccessPopulationExceptionView? included = null;
        var seen = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var domainEvent = record.Event;
            var metadata = domainEvent?.Metadata;
            if (record.Stream != stream || domainEvent is not AccessPopulationExceptionRecorded recorded ||
                metadata is null || metadata.EventId == Uuid.Empty || metadata.AggregateId != exceptionId ||
                metadata.AggregateVersion != 1 || metadata.OccurredOn == default || ++seen > 1 ||
                recorded.TenantId != tenantId || recorded.SystemInstanceId != systemId || recorded.Revision != 1)
                return InvalidException();
            var exception = await ReadContentAsync<AccessPopulationExceptionView>(tenantId,
                recorded.ExceptionContent, ct).ConfigureAwait(false);
            if (!ValidPopulationException(exception, systemId) || exception!.ExceptionId != exceptionId)
                return InvalidException();
            if (context.RecordedThrough is null ? exception.ApprovedAt <= through && metadata.OccurredOn <= through :
                await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false))
                included = exception;
        }

        return seen == 1
            ? Result<AccessPopulationExceptionView?>.Success(included)
            : InvalidException();
    }

    async ValueTask<T?> ReadContentAsync<T>(Uuid tenantId, ImmutableContentRef reference, CancellationToken ct)
        where T : class
    {
        try
        {
            return await content.ReadJsonAsync<T>(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ImmutableContentIntegrityException or JsonException)
        {
            return null;
        }
    }

    static bool ValidProposedExpectation(AccessExpectationView? expectation, Uuid systemId) =>
        expectation is not null && expectation.ExpectationId != Uuid.Empty &&
        expectation.SystemInstanceId == systemId &&
        expectation.Status == AccessExpectation.Proposed &&
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
        expectation.Status == AccessExpectation.Approved &&
        expectation.EffectiveFrom <= at &&
        (expectation.EffectiveUntil is null || at < expectation.EffectiveUntil) &&
        !expectations.Any(successor => successor.Status == AccessExpectation.Approved &&
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
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<LineHistory> InvalidLine() =>
        Result<LineHistory>.Failure(new RequestError(RequestErrorKind.Conflict,
            "An access expectation ledger is invalid or inconsistent."));

    static Result<AccessPopulationExceptionView?> InvalidException() =>
        Result<AccessPopulationExceptionView?>.Failure(new RequestError(RequestErrorKind.Conflict,
            "An access expectation ledger is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "A reviewed-system reference is invalid or does not match its access expectation ledger."));

    sealed record LineHistory(long Facts, IReadOnlyList<AccessExpectationView> Expectations,
        IReadOnlyList<AccessExpectationExceptionView> Exceptions);

    sealed record AccessExpectationHistory(long AsOfRevision,
        IReadOnlyList<AccessExpectationView> Expectations,
        IReadOnlyList<AccessExpectationExceptionView> Exceptions,
        IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions);
}
