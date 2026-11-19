using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Processes;

[Discriminator("bdgrz.platform.primary.reconcile-signal", 1)]
sealed record ReconcilePlatformPrimarySignal(NativeFactReference Source, ImmutableContentRef Intent,
    ulong SourcePosition, ulong TargetPosition, ulong TargetRevision) : IRequest<ReceiverOutcome>;
