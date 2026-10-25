using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The expectations and exceptions of one expectation line with their current states, folded from the facts of an
///     <see cref="AccessExpectation" /> and the immutable content they reference. Every expectation reports the line's
///     current revision, which is the value a caller sends as the next expected revision.
/// </summary>
public sealed class AccessExpectationLine
{
    readonly List<AccessExpectationView> _expectations = [];
    readonly List<AccessExpectationExceptionView> _exceptions = [];

    AccessExpectationLine(long revision) => Revision = revision;

    /// <summary>The logical revision of the line when it was read.</summary>
    public long Revision { get; }

    /// <summary>Every expectation on the line in proposal order.</summary>
    public IReadOnlyList<AccessExpectationView> Expectations => _expectations;

    /// <summary>Every exception recorded on the line in order.</summary>
    public IReadOnlyList<AccessExpectationExceptionView> Exceptions => _exceptions;

    /// <summary>The line of a root expectation that has no fact yet.</summary>
    public static AccessExpectationLine Empty { get; } = new(0);

    /// <summary>Folds the facts of a hydrated line, reading each fact's immutable content.</summary>
    public static async Task<AccessExpectationLine> ReadAsync(AccessExpectation owner, IDomainEventReader events,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(content);
        var line = new AccessExpectationLine(owner.Revision);
        foreach (var fact in await AccessExpectationHistory.ReadAsync(events, owner, ct).ConfigureAwait(false))
            switch (fact)
            {
                case AccessExpectationProposed proposed:
                    var expectation = await AccessReviewContent.ReadAsync<AccessExpectationView>(content,
                        tenantId, proposed.ExpectationContent, ct).ConfigureAwait(false);
                    if (expectation.ExpectationId != proposed.Operation.LogicalId ||
                        expectation.SystemInstanceId != proposed.SystemInstanceId || expectation.Revision != proposed.Revision ||
                        expectation.Status != AccessExpectation.Proposed || expectation.ProposedBy != proposed.Operation.Actor ||
                        expectation.ProposedAt != proposed.Operation.AcceptedAt ||
                        expectation.SupersedesExpectationId is { } predecessor &&
                        line.Find(predecessor) is not { Status: AccessExpectation.Approved })
                        throw new ImmutableContentIntegrityException("The expectation body differs from its exact proposal or predecessor.");
                    line._expectations.Add(expectation);
                    break;
                case AccessExpectationApproved approved:
                    var index = line._expectations.FindIndex(item => item.ExpectationId == approved.ExpectationId);
                    line._expectations[index] = line._expectations[index] with
                    {
                        Status = AccessExpectation.Approved,
                        ApprovedBy = approved.ApprovedBy,
                        ApprovedAt = approved.ApprovedAt,
                        SeparationOfDutiesWaiverId = approved.SeparationOfDutiesWaiverId,
                    };
                    break;
                case AccessExpectationExceptionRecorded recorded:
                    var exception = await AccessReviewContent.ReadAsync<AccessExpectationExceptionView>(
                        content, tenantId, recorded.ExceptionContent, ct).ConfigureAwait(false);
                    if (exception.ExceptionId != recorded.Operation.LogicalId ||
                        line.Find(exception.ExpectationId) is not { Status: AccessExpectation.Approved } ||
                        exception.ApprovedBy != recorded.Operation.Actor || exception.ApprovedAt != recorded.Operation.AcceptedAt ||
                        exception.ExpiresAt <= exception.ApprovedAt)
                        throw new ImmutableContentIntegrityException("The exception body differs from its exact approved expectation or native attribution.");
                    line._exceptions.Add(exception);
                    break;
            }

        for (var index = 0; index < line._expectations.Count; index++)
            line._expectations[index] = line._expectations[index] with { Revision = owner.Revision };
        return line;
    }

    /// <summary>One expectation of the line.</summary>
    public AccessExpectationView? Find(Uuid expectationId) =>
        _expectations.Find(expectation => expectation.ExpectationId == expectationId);
}
