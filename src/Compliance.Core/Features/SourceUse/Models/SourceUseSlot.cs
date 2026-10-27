using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.SourceUse.Models;

readonly record struct SourceUseSlot(Uuid Root, EventStreamAddress FirstOwner, ulong SourceGeneration, string Purpose)
{
    public static SourceUseSlot Of(EventStreamAddress owner, ReservationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Target != owner)
            throw new ArgumentException("The key names another target owner.", nameof(key));
        return new(key.RootOperationId, key.FirstOwner, key.SourceGeneration, key.Purpose);
    }

    public static SourceUseSlot Of(EventStreamAddress owner, SourceUsePurpose purpose, ISourceUseFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return Of(owner, purpose, fact.Operation, fact.Generation, fact.Owner);
    }

    public static SourceUseSlot Of(EventStreamAddress owner, SourceUsePurpose purpose, OperationProvenance operation,
        long generation, EventStreamAddress? factOwner = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (factOwner is not null && factOwner != owner || operation.Owner != owner || generation < 0 ||
            operation.RootOperationId == Uuid.Empty)
            throw new InvalidOperationException("A source use fact belongs to its own owner stream and root operation.");
        return new(operation.RootOperationId, operation.FirstOwner ?? operation.Owner, (ulong)generation,
            SourceUseReservations.PurposeName(purpose));
    }
}
