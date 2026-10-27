using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     What the irreversible authorization of an acceptance or staff assignment retains: the manifest of the exact
///     practice reservation grants and the manifest of the exact service epoch and source use grants it relied on.
///     Both list <see cref="Bdgrz.Compliance.Features.NativeOperations.Models.NativeFactReference" /> items in canonical stream key
///     order of their owners.
/// </summary>
public sealed record EngagementAuthorizationProofs(ImmutableContentRef Reservations, ImmutableContentRef SourceUses);
