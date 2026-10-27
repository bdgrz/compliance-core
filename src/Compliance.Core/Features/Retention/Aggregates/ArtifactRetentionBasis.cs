using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Aggregates;

/// <summary>One immutable seven-year basis for one exact tenant artifact content identity.</summary>
public sealed class ArtifactRetentionBasis : Aggregate
{
    public const string Area = "artifact-retention-bases";
    readonly ArtifactRetentionSource _source;

    public ArtifactRetentionBasis(ArtifactRetentionSource source, Uuid selectedLogicalId = default)
        : base(IdentityOf(source), new EventStreamAddress(source.TenantId.ToString(), Area, IdentityOf(source).ToString()))
    {
        _source = source;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ArtifactRetentionBasisRecorded>(Apply);
    }

    public static Uuid IdentityOf(ArtifactRetentionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!ArtifactRetentionRules.ValidSource(source))
            throw new ArgumentException("A retention basis requires an exact supported tenant content identity.", nameof(source));
        return AggregateIdentity.Create("ArtifactRetentionBasis", source.TenantId, source.SourceKind,
            source.SourceId, source.ContentSha256);
    }

    public NativeAcceptance Acceptance { get; }
    public long Revision => Basis is null ? 0 : 1;
    public ArtifactRetentionSource Source => _source;
    public ArtifactRetentionBasisRecorded? Basis { get; private set; }

    public Result PrepareRecord(ArtifactRetentionSourceSnapshot source, long expectedRevision,
        DateOnly? periodStart, DateOnly? periodEnd, string reason)
    {
        if (source.Source != _source || source.Position == 0)
            return ArtifactRetentionRules.Conflict("The basis requires its exact durable source content.");
        if (expectedRevision < 0 || !ArtifactRetentionRules.ValidReason(reason))
            return ArtifactRetentionRules.Invalid("A basis requires a non-negative revision and bounded reason.");
        if (expectedRevision != Revision || Basis is not null)
            return ArtifactRetentionRules.Conflict("This exact content already has a basis or its revision changed.");
        if ((periodStart is null) != (periodEnd is null))
            return InvalidPeriod();
        if (_source.SourceKind == "evidence_artifact")
        {
            if (source.PeriodStart is not { } nativeStart || source.PeriodEnd is not { } nativeEnd ||
                periodStart is { } first && first != nativeStart || periodEnd is { } last && last != nativeEnd)
                return InvalidPeriod();
            periodStart = nativeStart;
            periodEnd = nativeEnd;
        }
        return periodStart is { } start && periodEnd is { } end && ArtifactRetentionRules.ValidPeriod(start, end)
            ? Result.Success : InvalidPeriod();
    }

    public Result Record(OperationProvenance operation, ArtifactRetentionSourceSnapshot source,
        DateOnly? periodStart, DateOnly? periodEnd, string reason,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (ArtifactRetentionRules.Replay(Acceptance, operation) is { } retry)
            return retry;
        if (operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue ||
            !ArtifactRetentionRules.ValidOperation(operation, Stream, "RecordArtifactRetentionBasis", (long)expected))
            return ArtifactRetentionRules.Invalid("The basis requires its exact native root operation.");
        var prepared = PrepareRecord(source, (long)expected, periodStart, periodEnd, reason);
        if (!prepared.IsSuccess)
            return prepared;
        var first = _source.SourceKind == "evidence_artifact" ? source.PeriodStart!.Value : periodStart!.Value;
        var last = _source.SourceKind == "evidence_artifact" ? source.PeriodEnd!.Value : periodEnd!.Value;
        RaiseEvent(new ArtifactRetentionBasisRecorded(_source, 1, source.Position, ArtifactRetentionRules.Policy,
            first, last, last.AddYears(7), ArtifactRetentionRules.BasisKind(_source.SourceKind), reason.Trim(),
            operation.Actor, operation.AcceptedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(ArtifactRetentionBasisRecorded ev)
    {
        if (Basis is not null || ev.Source != _source || ev.Revision != 1 || ev.SourcePosition == 0 ||
            ev.Policy != ArtifactRetentionRules.Policy || !ArtifactRetentionRules.ValidPeriod(ev.PeriodStart, ev.PeriodEnd) ||
            ev.RetainsThrough != ev.PeriodEnd.AddYears(7) || ev.BasisKind != ArtifactRetentionRules.BasisKind(_source.SourceKind) ||
            !ArtifactRetentionRules.ValidReason(ev.Reason) || ev.Reason != ev.Reason.Trim() ||
            ev.RecordedBy != ev.Operation.Actor || ev.RecordedAt != ev.Operation.AcceptedAt ||
            !ArtifactRetentionRules.ValidOperation(ev.Operation, Stream, "RecordArtifactRetentionBasis", 0) ||
            ev.FirstResponse is null || ev.OperationResult is null)
            throw new InvalidOperationException("A retention basis is one exact attributed native content decision.");
        Acceptance.Observe(ev);
        Basis = ev;
    }

    static Result InvalidPeriod() => ArtifactRetentionRules.Invalid(
        "Retention requires the exact explicit supported period within policy date bounds.");
}
