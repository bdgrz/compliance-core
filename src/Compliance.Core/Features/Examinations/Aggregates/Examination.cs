using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Kernel.Services;

namespace Bdgrz.Compliance.Features.Examinations.Aggregates;

/// <summary>Owns one examination's permanent program/type, setup, named signers and terminal close.</summary>
public sealed class Examination : Aggregate
{
    public const string Area = "examinations";
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, (string Digest, Uuid Actor)> _accepted = [];
    public ExaminationView? Current { get; private set; }
    public long Revision => Current?.Revision ?? 0;

    public Examination(Uuid tenantId, Uuid examinationId)
        : base(examinationId, new EventStreamAddress(tenantId.ToString(), Area, examinationId.ToString()))
    {
        _tenantId = tenantId;
        On<ExaminationOpened>(ApplyFact);
        On<ExaminationAuditorChanged>(ApplyFact);
        On<ExaminationTargetDatesSet>(ApplyFact);
        On<ExaminationDatesConfirmed>(ApplyFact);
        On<ExaminationSignersNamed>(ApplyFact);
        On<ExaminationClosed>(ApplyFact);
    }

    public bool HasAccepted(Uuid commandId) => _accepted.ContainsKey(commandId);

    public bool IsNamedSigner(Uuid memberId) => memberId != Uuid.Empty && Current is { } state &&
        (state.CeoSignerMemberId == memberId || state.CisoSignerMemberId == memberId);

    public CommandFailure? Open(OpenExamination request, Uuid actor, string display, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (TryReplay(request.CommandId, digest, actor, out var retry))
            return retry;
        if (Current is not null)
            return CommandFailure.StateConflict("The examination already exists.");
        if (!Scope(request.TenantId, request.ProgramId, request.ExaminationId) ||
            request.ExaminationType is not ("type_i" or "type_ii") || !ValidAuditor(request.Auditor) ||
            !ValidAcceptance(request.CommandId, actor, display, at))
            return Invalid();
        var state = new ExaminationView(_tenantId, request.ProgramId, Id, request.ExaminationType,
            "open", 1, request.Auditor, new ExaminationDates(null, null, null), null, null, null, null, null);
        RaiseEvent(new ExaminationOpened(_tenantId, Id, request.CommandId, 0, digest, actor, display.Trim(), at, state));
        return null;
    }

    public CommandFailure? ChangeAuditor(ChangeExaminationAuditor request, Uuid actor, string display, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (TryReplay(request.CommandId, digest, actor, out var retry))
            return retry;
        if (Guard(request.TenantId, request.ProgramId, request.ExaminationId, request.ExpectedRevision,
                request.CommandId, actor, display, at) is { } failure)
            return failure;
        if (!ValidAuditor(request.Auditor))
            return Invalid();
        if (Current!.Auditor == request.Auditor)
            return null;
        RaiseEvent(new ExaminationAuditorChanged(_tenantId, Id, request.CommandId, request.ExpectedRevision,
            digest, actor, display.Trim(), at, Current with { Auditor = request.Auditor, Revision = Revision + 1 }));
        return null;
    }

    public CommandFailure? SetTargetDates(SetExaminationTargetDates request, Uuid actor, string display, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (TryReplay(request.CommandId, digest, actor, out var retry))
            return retry;
        if (Guard(request.TenantId, request.ProgramId, request.ExaminationId, request.ExpectedRevision,
                request.CommandId, actor, display, at) is { } failure)
            return failure;
        if (!ValidDates(Current!.ExaminationType, request.Dates))
            return Invalid();
        if (Current.TargetDates == request.Dates)
            return null;
        RaiseEvent(new ExaminationTargetDatesSet(_tenantId, Id, request.CommandId, request.ExpectedRevision,
            digest, actor, display.Trim(), at, Current with { TargetDates = request.Dates, Revision = Revision + 1 }));
        return null;
    }

    public CommandFailure? ConfirmDates(ConfirmExaminationDates request, Uuid actor, string display, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (TryReplay(request.CommandId, digest, actor, out var retry))
            return retry;
        if (Guard(request.TenantId, request.ProgramId, request.ExaminationId, request.ExpectedRevision,
                request.CommandId, actor, display, at) is { } failure)
            return failure;
        if (!ValidDates(Current!.ExaminationType, request.Dates) ||
            request.Dates is { AsOfDate: null, PeriodStart: null, PeriodEnd: null } ||
            !Text(request.EvidenceReference, 2000))
            return Invalid();
        var evidence = request.EvidenceReference.Trim();
        if (Current.ConfirmedDates == request.Dates && Current.ConfirmationEvidenceReference == evidence)
            return null;
        RaiseEvent(new ExaminationDatesConfirmed(_tenantId, Id, request.CommandId, request.ExpectedRevision,
            digest, actor, display.Trim(), at, Current with
            { ConfirmedDates = request.Dates, ConfirmationEvidenceReference = evidence, Revision = Revision + 1 }));
        return null;
    }

    public CommandFailure? NameSigners(NameExaminationSigners request, Uuid actor, string display, DateTimeOffset at)
    {
        var digest = AuthoredIntentDigest.Compute(request);
        if (TryReplay(request.CommandId, digest, actor, out var retry))
            return retry;
        if (Guard(request.TenantId, request.ProgramId, request.ExaminationId, request.ExpectedRevision,
                request.CommandId, actor, display, at) is { } failure)
            return failure;
        if (request.CeoSignerMemberId == Uuid.Empty || request.CisoSignerMemberId == Uuid.Empty)
            return Invalid();
        if (Current!.CeoSignerMemberId == request.CeoSignerMemberId && Current.CisoSignerMemberId == request.CisoSignerMemberId)
            return null;
        RaiseEvent(new ExaminationSignersNamed(_tenantId, Id, request.CommandId, request.ExpectedRevision,
            digest, actor, display.Trim(), at, Current with
            { CeoSignerMemberId = request.CeoSignerMemberId, CisoSignerMemberId = request.CisoSignerMemberId, Revision = Revision + 1 }));
        return null;
    }

    public CommandFailure? Close(ExaminationClosure closure, Uuid actor, string display, DateTimeOffset at)
    {
        if (Current is null)
            return CommandFailure.MissingRecord("The examination was not found.");
        if (Current.Status == "closed")
            return null;
        if (closure is null || closure.SignOffId == Uuid.Empty || closure.SignOffEventId == Uuid.Empty ||
            closure.OutcomeId == Uuid.Empty || !ValidAcceptance(closure.SignOffEventId, actor, display, at))
            return Invalid();
        RaiseEvent(new ExaminationClosed(_tenantId, Id, closure.SignOffEventId, Revision,
            AuthoredIntentDigest.Compute(closure), actor, display.Trim(), at,
            Current with { Status = "closed", Closure = closure, Revision = Revision + 1 }));
        return null;
    }

    CommandFailure? Guard(Uuid tenant, Uuid program, Uuid examination, long expectedRevision,
        Uuid command, Uuid actor, string display, DateTimeOffset at)
    {
        if (Current is null || !Scope(tenant, program, examination) || Current.ProgramId != program)
            return CommandFailure.MissingRecord("The examination was not found in this program.");
        if (Current.Status != "open" || Revision != expectedRevision)
            return CommandFailure.StateConflict("The examination must be open at its expected revision.");
        return ValidAcceptance(command, actor, display, at) ? null : Invalid();
    }

    bool TryReplay(Uuid command, string digest, Uuid actor, out CommandFailure? failure)
    {
        failure = null;
        if (!_accepted.TryGetValue(command, out var accepted))
            return false;
        failure = accepted.Digest == digest && accepted.Actor == actor ? null :
            CommandFailure.StateConflict("This command identity already accepted a different actor or authored intent.");
        return true;
    }

    void ApplyFact<T>(T fact) where T : DomainEvent, IExaminationSetupFact
    {
        var state = fact.State;
        if (fact.TenantId != _tenantId || fact.SubjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ExaminationId != Id || state.ProgramId == Uuid.Empty ||
            state.ExaminationType is not ("type_i" or "type_ii") || state.Revision != Revision + 1 ||
            fact.OriginalExpectedRevision != Revision || !Digest(fact.AuthoredIntentSha256) ||
            !ValidAcceptance(fact.CommandId, fact.ActorMemberId, fact.ActorDisplay, fact.OccurredAt) ||
            fact.ActorDisplay != fact.ActorDisplay.Trim() || _accepted.ContainsKey(fact.CommandId) ||
            !ValidAuditor(state.Auditor) || !ValidDates(state.ExaminationType, state.TargetDates) ||
            state.ConfirmedDates is { } confirmed && (!ValidDates(state.ExaminationType, confirmed) ||
                confirmed is { AsOfDate: null, PeriodStart: null, PeriodEnd: null } ||
                !Text(state.ConfirmationEvidenceReference, 2000)) ||
            state.ConfirmedDates is null && state.ConfirmationEvidenceReference is not null ||
            !ValidTransition(fact))
            throw new InvalidOperationException("The examination setup fact violates its identity or lifecycle.");
        _accepted.Add(fact.CommandId, (fact.AuthoredIntentSha256, fact.ActorMemberId));
        Current = state;
    }

    bool ValidTransition(IExaminationSetupFact fact)
    {
        var next = fact.State;
        if (Current is null)
            return fact is ExaminationOpened && next.Status == "open" && next.Revision == 1 &&
                next.TargetDates == new ExaminationDates(null, null, null) && next.ConfirmedDates is null &&
                next.CeoSignerMemberId is null && next.CisoSignerMemberId is null && next.Closure is null;
        if (Current.Status != "open" || next.ProgramId != Current.ProgramId || next.ExaminationType != Current.ExaminationType)
            return false;
        var expected = Current with { Revision = Current.Revision + 1 };
        return fact switch
        {
            ExaminationAuditorChanged => next == (expected with { Auditor = next.Auditor }),
            ExaminationTargetDatesSet => next == (expected with { TargetDates = next.TargetDates }),
            ExaminationDatesConfirmed => next == (expected with
            { ConfirmedDates = next.ConfirmedDates, ConfirmationEvidenceReference = next.ConfirmationEvidenceReference }),
            ExaminationSignersNamed => next.CeoSignerMemberId is { } ceo && ceo != Uuid.Empty &&
                next.CisoSignerMemberId is { } ciso && ciso != Uuid.Empty && next == (expected with
                { CeoSignerMemberId = ceo, CisoSignerMemberId = ciso }),
            ExaminationClosed => next.Closure is { } closure && closure.SignOffId != Uuid.Empty &&
                closure.SignOffEventId == fact.CommandId && closure.OutcomeId != Uuid.Empty &&
                next == (expected with { Status = "closed", Closure = closure }),
            _ => false,
        };
    }

    bool Scope(Uuid tenant, Uuid program, Uuid examination) => _tenantId != Uuid.Empty && tenant == _tenantId &&
        program != Uuid.Empty && examination == Id;
    static bool ValidAcceptance(Uuid command, Uuid actor, string? display, DateTimeOffset at) =>
        command != Uuid.Empty && actor != Uuid.Empty && Text(display, 200) && at != default;
    static bool ValidAuditor(ExaminationAuditor? auditor) => auditor switch
    {
        { Kind: "unknown", ServiceEngagementId: null, FirmName: null } => true,
        { Kind: "external", ServiceEngagementId: null, FirmName: { } name } => Text(name, 200) && name == name.Trim(),
        { Kind: "attest", ServiceEngagementId: { } engagement, FirmName: null } => engagement != Uuid.Empty,
        _ => false,
    };
    static bool ValidDates(string kind, ExaminationDates? dates) => dates is not null &&
        dates.AsOfDate != default(DateOnly) && dates.PeriodStart != default(DateOnly) && dates.PeriodEnd != default(DateOnly) &&
        (kind == "type_i" ? dates.PeriodStart is null && dates.PeriodEnd is null :
            dates.AsOfDate is null && (dates.PeriodStart is null || dates.PeriodEnd is null || dates.PeriodStart <= dates.PeriodEnd));
    static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;
    static bool Digest(string? value) => value is { Length: 64 } && value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    static CommandFailure Invalid() => CommandFailure.InvalidContent("The examination setup decision has invalid dates, auditor, scope or attribution.");
}
