using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>CompleteAdministratorWithdrawal is a stage receiver of AdministratorTransitionProcess, driven by the process record after its apply stage; this reactor name is not subscribed to any stream.</summary>
public static class AdministratorWithdrawalCompletionReactor
{
    /// <summary>The catalog receiver this reactor name maps to.</summary>
    public const string Receiver = AdministratorReceivers.CompleteAdministratorWithdrawal;
}
