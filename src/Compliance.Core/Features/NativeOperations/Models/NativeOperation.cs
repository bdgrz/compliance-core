using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>
/// One root native admission on a first owner. <paramref name="Hydrate"/> builds a fresh owner instance selected
/// for the binding's logical id; <paramref name="Prepare"/> checks guards against hydrated state and shapes the
/// first response without raising; <paramref name="Apply"/> re-checks and raises the native fact carrying the
/// frozen first response.
/// </summary>
public sealed record NativeOperation<TAggregate>(AcceptedOperationBinding Binding, Func<TAggregate> Hydrate,
    Func<TAggregate, NativeAcceptance> Acceptance,
    Func<TAggregate, CancellationToken, ValueTask<Result<FirstResponseDescriptor>>> Prepare,
    Func<TAggregate, ImmutableContentRef, Result> Apply) where TAggregate : Aggregate
{
    /// <summary>Adapts synchronous preparation while preserving acceptance-before-preparation ordering.</summary>
    public NativeOperation(AcceptedOperationBinding binding, Func<TAggregate> hydrate,
        Func<TAggregate, NativeAcceptance> acceptance, Func<TAggregate, Result<FirstResponseDescriptor>> prepare,
        Func<TAggregate, ImmutableContentRef, Result> apply)
        : this(binding, hydrate, acceptance, (aggregate, _) => ValueTask.FromResult(prepare(aggregate)), apply)
    {
    }
}
