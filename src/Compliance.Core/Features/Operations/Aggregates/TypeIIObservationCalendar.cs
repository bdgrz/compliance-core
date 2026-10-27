using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Aggregates;

/// <summary>One compact program range guard; live and terminal authority are verified immutable content.</summary>
public sealed class TypeIIObservationCalendar : Aggregate
{
    public const string Area = "type-ii-observation-calendars";
    public TypeIIObservationCalendar(Uuid tenantId, Uuid programId, Uuid selectedLogicalId = default)
        : base(programId, new EventStreamAddress(tenantId.ToString(), Area, programId.ToString()))
    {
        TenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TypeIIPeriodRangeAuthorized>(Apply);
        On<TypeIIPeriodRangeSettled>(Apply);
    }
    public Uuid TenantId { get; }
    public NativeAcceptance Acceptance { get; }
    public TypeIIObservationCalendarHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentAuthority { get; private set; }
    public long Revision => Header?.Revision ?? 0;
    public long ReservationGeneration => Header?.ReservationGeneration ?? 0;
    public long NativeSequence { get; private set; }
    public Result Append(DomainEvent value, TypeIICalendarPrepared prepared, long nativeSequence, ulong physicalPosition)
    {
        if (NativeSequence != nativeSequence || CommittedStreamPosition != physicalPosition)
            return Failure("The calendar changed while its immutable range authority was prepared.", true);
        try
        {
            var before = prepared.Before;
            if (Header is null ? ContentDigest.OfJson(before) != ContentDigest.OfJson(TypeIICalendarContent.Empty(TenantId, Id)) :
                TypeIICalendarContent.Header(before) != Header || CurrentAuthority?.Sha256 != ContentDigest.OfJson(before))
                return Failure("The calendar preparation has another exact current authority cut.");
            var expected = TypeIICalendarContent.Fact(prepared);
            if (!TypeIICalendarContent.Verify(prepared) || value.GetType() != expected.GetType() || !value.Equals(expected))
                return Failure("The calendar mutation differs from its verified range decision.");
            Validate(value);
            if (Acceptance.SelectedLogicalId != Uuid.Empty &&
                (((INativeOperationFact)value).Operation.LogicalId != Acceptance.SelectedLogicalId || Acceptance.SelectedFact is not null))
                return Failure("The calendar append does not select one new logical acceptance.");
            RaiseEvent(value);
            return Result.Success;
        }
        catch (ImmutableContentIntegrityException)
        {
            return Failure("The calendar terminal membership and live range decision cannot be verified.");
        }
        catch (InvalidOperationException)
        {
            return Failure("The calendar native mutation has another owner or sequence.");
        }
    }
    void Apply(TypeIIPeriodRangeAuthorized value) => ApplyFact(value);
    void Apply(TypeIIPeriodRangeSettled value) => ApplyFact(value);
    void ApplyFact(DomainEvent value)
    {
        Validate(value);
        Header = TypeIICalendarContent.FactHeader(value);
        CurrentAuthority = TypeIICalendarContent.FactAuthority(value);
        NativeSequence++;
        Acceptance.Observe(value);
    }
    void Validate(DomainEvent value)
    {
        var header = TypeIICalendarContent.FactHeader(value);
        var authority = TypeIICalendarContent.FactAuthority(value);
        var native = (INativeOperationFact)value;
        if (header is null || !TypeIICalendarContent.Reference(authority, TypeIICalendarContent.AuthoritySchema) ||
            header.TenantId != TenantId || header.ProgramId != Id || header.Revision != Revision + 1 ||
            native.Operation is not { } operation || operation.Owner != Stream || operation.Actor is null || operation.LogicalId == Uuid.Empty || operation.RootOperationId == Uuid.Empty ||
            operation.AuthoredIntentSha256 is not { Length: 64 } || !operation.AuthoredIntentSha256.All(Uri.IsHexDigit) ||
            header.LiveCount < 0 || header.TerminalCount < 0 || DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
            throw new InvalidOperationException("The calendar native fact has another exact authority scope or sequence.");
        var live = Header?.LiveCount ?? 0;
        var terminal = Header?.TerminalCount ?? 0;
        var valid = value switch
        {
            TypeIIPeriodRangeAuthorized fact => fact.Owner == Stream && fact.Intent is not null && fact.Revision == header.Revision && fact.Generation == TypeIIRangeEffects.Generation &&
                fact.PeriodId != Uuid.Empty && fact.NewRange is { IsValid: true } &&
                fact.Kind is TypeIIRangeOperationKind.Opening or TypeIIRangeOperationKind.Amendment &&
                (fact.Kind == TypeIIRangeOperationKind.Opening) == (fact.PriorOperationId is null) &&
                header.ReservationGeneration == ReservationGeneration + 1 && header.LiveCount == live + 1 && header.TerminalCount == terminal,
            TypeIIPeriodRangeSettled fact => fact.Owner == Stream && fact.Intent is not null && fact.Revision == header.Revision && fact.Generation == TypeIIRangeEffects.Generation &&
                fact.PeriodId != Uuid.Empty && fact.TargetOperationId != Uuid.Empty &&
                fact.Settlement is TypeIIRangeSettlementKind.Activated or TypeIIRangeSettlementKind.Released or TypeIIRangeSettlementKind.Fenced &&
                (fact.ReleasedOperationId is null || fact.Settlement == TypeIIRangeSettlementKind.Activated) && header.ReservationGeneration == ReservationGeneration &&
                header.LiveCount == live - (fact.Settlement == TypeIIRangeSettlementKind.Released || fact.ReleasedOperationId is not null ? 1 : 0) &&
                header.TerminalCount == terminal + (fact.Settlement is TypeIIRangeSettlementKind.Released or TypeIIRangeSettlementKind.Fenced || fact.ReleasedOperationId is not null ? 1 : 0),
            _ => false,
        };
        if (!valid)
            throw new InvalidOperationException("The calendar native counters differ from their exact range transition.");
    }
    static Result Failure(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
