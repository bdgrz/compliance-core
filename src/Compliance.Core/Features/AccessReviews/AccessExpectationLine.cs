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
    public static async Task<AccessExpectationLine> ReadAsync(AccessExpectation owner,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(content);
        var line = new AccessExpectationLine(owner.Revision);
        foreach (var fact in owner.Facts)
            switch (fact)
            {
                case AccessExpectationProposed proposed:
                    line._expectations.Add(await AccessReviewContent.ReadAsync<AccessExpectationView>(content,
                        tenantId, proposed.ExpectationContent, ct).ConfigureAwait(false));
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
                    line._exceptions.Add(await AccessReviewContent.ReadAsync<AccessExpectationExceptionView>(
                        content, tenantId, recorded.ExceptionContent, ct).ConfigureAwait(false));
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
