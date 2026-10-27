using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Transient prepared append; full views never enter the command owner's retained state.</summary>
public sealed record FrozenTypeIIPeriodMutation(TypeIIPeriodView? Before, TypeIIPeriodView After,
    object RuleMutation, ImmutableContentRef CurrentContent, ImmutableContentRef? Change);
