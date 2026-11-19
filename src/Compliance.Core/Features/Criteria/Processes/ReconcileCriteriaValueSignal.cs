using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

/// <summary>Reconciles one durable source signal; only a process worker may dispatch it.</summary>
[Discriminator("bdgrz.criteria.reconcile-value-signal", 1)]
sealed record ReconcileCriteriaValueSignal(NativeFactReference Source, ulong Position,
    ImmutableContentRef Intent) : IRequest<ReceiverOutcome>;
