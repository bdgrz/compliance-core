using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

/// <summary>Minimal first owner whose single native fact is the admission; edits advance its revision.</summary>
sealed class AcceptanceProbe : Aggregate
{
    public const string Realm = "tenant-a";

    public AcceptanceProbe(Uuid probeId, Uuid selectedLogicalId = default)
        : base(probeId, StreamOf(probeId))
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ProbeAdmitted>(Apply);
        On<ProbeEdited>(Apply);
    }

    public NativeAcceptance Acceptance { get; }

    public string? Payload { get; private set; }

    public int Admissions { get; private set; }

    public static EventStreamAddress StreamOf(Uuid probeId) => new(Realm, "probes", probeId.ToString());

    public Result Admit(OperationProvenance operation, ImmutableContentRef firstResponse, string payload,
        ulong expectedRevision)
    {
        if (Version != expectedRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The probe revision changed.", false));
        RaiseEvent(new ProbeAdmitted(Id, payload, operation, firstResponse));
        return Result.Success;
    }

    public Result Edit(string payload)
    {
        RaiseEvent(new ProbeEdited(Id, payload));
        return Result.Success;
    }

    void Apply(ProbeAdmitted ev)
    {
        Acceptance.Observe(ev);
        Payload = ev.Payload;
        Admissions++;
    }

    void Apply(ProbeEdited ev) => Payload = ev.Payload;
}
