using System.Diagnostics.CodeAnalysis;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One explicit missing-population exception for an exact system instance, with its reason and future expiry. It
///     is an independent owner created once by its immutable exception id: there is no shared system ledger revision,
///     so unrelated exceptions never contend. Expiry affects eligibility at authoritative read time and needs no
///     further fact. The complete <see cref="AccessPopulationExceptionView" /> is verified immutable content.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "AccessPopulationException is the canonical business term for an approved missing-population exception.")]
public sealed class AccessPopulationException : Aggregate
{
    /// <summary>Stream area of every missing-population exception (the realm is the tenant).</summary>
    public const string Area = "access-population-exceptions";

    readonly Uuid _tenantId;
    AccessPopulationExceptionRecorded? _recorded;

    /// <summary>Opens the exception with a known id, selecting the logical operation to resolve.</summary>
    public AccessPopulationException(Uuid tenantId, Uuid exceptionId, Uuid selectedLogicalId = default)
        : base(exceptionId, new EventStreamAddress(tenantId.ToString(), Area, exceptionId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessPopulationExceptionRecorded>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The system instance this exception belongs to, once recorded.</summary>
    public Uuid? SystemInstanceId => _recorded?.SystemInstanceId;

    /// <summary>The recorded exception fact, once present.</summary>
    public AccessPopulationExceptionRecorded? Recorded => _recorded;

    /// <summary>The logical revision: one once recorded, otherwise zero.</summary>
    public long Revision => _recorded is null ? 0 : 1;

    /// <summary>Checks the creation guards without raising anything.</summary>
    public Result PrepareRecord(long expectedRevision)
    {
        if (_recorded is not null)
            return AccessReviewOperation.Failure(RequestErrorKind.Conflict,
                "The population exception already exists with different content.");
        return expectedRevision == 0
            ? Result.Success
            : AccessReviewOperation.Failure(RequestErrorKind.Conflict,
                $"A new population exception expects revision 0; the request names {expectedRevision}.");
    }

    /// <summary>Records the exception as the root native fact of its operation.</summary>
    public Result Record(OperationProvenance operation, Uuid systemInstanceId, ImmutableContentRef exceptionContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(exceptionContent);
        if (operation.Owner != Stream || operation.LogicalId != Id || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "A population exception is created by a root operation of its own stream with an expected revision.");
        var prepared = PrepareRecord((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessPopulationExceptionRecorded(_tenantId, systemInstanceId, 1, exceptionContent, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(AccessPopulationExceptionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is not null || ev.TenantId != _tenantId || ev.Revision != 1 || ev.Operation.Owner != Stream || ev.Operation.LogicalId != Id ||
            ev.FirstResponse is null)
            throw new InvalidOperationException("A population exception is recorded once, by its own root operation.");
        _recorded = ev;
    }
}
