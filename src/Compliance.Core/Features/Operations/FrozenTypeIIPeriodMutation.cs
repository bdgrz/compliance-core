using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Transient prepared append; full views never enter the command owner's retained state.</summary>
public sealed record FrozenTypeIIPeriodMutation(TypeIIPeriodView? Before, TypeIIPeriodView After,
    object RuleMutation, ImmutableContentRef CurrentContent, ImmutableContentRef? Change);
