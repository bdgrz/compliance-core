using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projections;

/// <summary>One immutable capture of a person's authoritative membership correlation for this evaluation.</summary>
public sealed record PolicyAcknowledgementPersonSnapshot(Uuid TenantId, Uuid PersonId, bool IsCreated,
    long Revision, ulong CommittedStreamPosition, Uuid? CorrelatedUserId)
{
    public static PolicyAcknowledgementPersonSnapshot Capture(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return new(Uuid.Parse(person.Stream.Realm, null), person.Id, person.IsCreated, person.Revision,
            person.CommittedStreamPosition, person.CorrelatedUserId);
    }
}
