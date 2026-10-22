using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.raised", 2)]
public sealed record FindingRaisedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    FindingSource Source, string Title, string Description, string Severity,
    string AffectedScope, Uuid OwnerMemberId, DateOnly DueOn, IReadOnlyList<FindingLink> Links,
    ActorReference RaisedBy, DateTimeOffset RaisedAt, string CreationKind = "manual",
    string? InitialActionSha256 = null) : DomainEvent
{
    readonly IReadOnlyList<FindingLink> _links = Array.AsReadOnly(Links.ToArray());
    public IReadOnlyList<FindingLink> Links
    {
        get => _links;
        init => _links = Array.AsReadOnly(value.ToArray());
    }
}
