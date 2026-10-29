using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

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
