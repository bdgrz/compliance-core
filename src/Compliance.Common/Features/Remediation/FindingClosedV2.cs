using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.closed", 2)]
public sealed record FindingClosedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    FindingClosureView Closure) : DomainEvent
{
    readonly FindingClosureView _closure = Freeze(Closure);
    public FindingClosureView Closure
    {
        get => _closure;
        init => _closure = Freeze(value);
    }
    static FindingClosureView Freeze(FindingClosureView value) => value with
    {
        ResolutionEvidence = Array.AsReadOnly(value.ResolutionEvidence.ToArray()),
    };
}
