using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
/// One root native admission on a first owner. <paramref name="Hydrate"/> builds a fresh owner instance selected
/// for the binding's logical id; <paramref name="Prepare"/> checks guards against hydrated state and shapes the
/// first response without raising; <paramref name="Apply"/> re-checks and raises the native fact carrying the
/// frozen first response.
/// </summary>
public sealed record NativeOperation<TAggregate>(AcceptedOperationBinding Binding, Func<TAggregate> Hydrate,
    Func<TAggregate, NativeAcceptance> Acceptance, Func<TAggregate, Result<FirstResponseDescriptor>> Prepare,
    Func<TAggregate, ImmutableContentRef, Result> Apply) where TAggregate : Aggregate;
