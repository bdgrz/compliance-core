using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Processes;

[Discriminator("bdgrz.criteria.reconcile-entry-signal", 1)]
sealed record ReconcileCriteriaEntrySignal(NativeFactReference Source, ulong Position,
    ImmutableContentRef Intent) : IRequest<ReceiverOutcome>;
