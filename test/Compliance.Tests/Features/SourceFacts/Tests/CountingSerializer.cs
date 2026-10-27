using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

/// <summary>Counts deserialization calls so tests can prove foreign content is never materialized.</summary>
sealed class CountingSerializer(IDomainEventSerializer inner) : IDomainEventSerializer
{
    public int Deserializations { get; private set; }

    public ReadOnlyMemory<byte> Serialize(DomainEvent ev) => inner.Serialize(ev);

    public DomainEvent Deserialize(ReadOnlyMemory<byte> data)
    {
        Deserializations++;
        return inner.Deserialize(data);
    }
}
