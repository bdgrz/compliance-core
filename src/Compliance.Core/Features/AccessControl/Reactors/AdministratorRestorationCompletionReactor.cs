namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>CompleteAdministratorRestoration is a stage receiver of AdministratorTransitionProcess, driven by the process record after its apply stage; this reactor name is not subscribed to any stream.</summary>
public static class AdministratorRestorationCompletionReactor
{
    /// <summary>The catalog receiver this reactor name maps to.</summary>
    public const string Receiver = AdministratorReceivers.CompleteAdministratorRestoration;
}
