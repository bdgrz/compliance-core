using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Reactors;
namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The catalog reactor names of the identity recovery process and the receiver each one maps to. Only
///     <see cref="IdentityRecoveryAcceptanceReactor" /> subscribes to a stream: it starts the process record from the
///     claimed challenge. Every other name is a stage receiver the process record dispatches after the stage before
///     it, so no further subscription exists; each name resolves to its receiver through <see cref="ReceiverOf" />.
/// </summary>
public static class IdentityRecoveryReactors
{
    public const string Acceptance = nameof(IdentityRecoveryAcceptanceReactor);
    public const string Continuation = "IdentityRecoveryContinuationReactor";
    public const string ProtectedAcceptance = "ProtectedIdentityRecoveryAcceptanceReactor";
    public const string Replacement = "IdentityRecoveryReplacementReactor";
    public const string OldRevocation = "IdentityRecoveryOldRevocationReactor";
    public const string Completion = "IdentityRecoveryCompletionReactor";
    public const string ProtectionRelease = "IdentityRecoveryProtectionReleaseReactor";
    public const string EmailCompletion = "IdentityRecoveryEmailCompletionReactor";
    public const string CompletionNotice = "IdentityRecoveryCompletionNoticeReactor";

    /// <summary>The catalog receiver a reactor name maps to.</summary>
    public static string ReceiverOf(string reactor) => reactor switch
    {
        Acceptance => IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation,
        Continuation => IdentityRecoveryReceivers.ProtectRecoveryIdentity,
        ProtectedAcceptance => IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery,
        Replacement => IdentityRecoveryReceivers.RegisterRecoveredIdentity,
        OldRevocation => IdentityRecoveryReceivers.RevokeRecoveredOldIdentity,
        Completion => IdentityRecoveryReceivers.CompleteAcceptedIdentityRecovery,
        ProtectionRelease => IdentityRecoveryReceivers.SettleRecoveryIdentityProtection,
        EmailCompletion => IdentityRecoveryReceivers.CompleteEmailRecoveryChallenge,
        CompletionNotice => IdentityRecoveryReceivers.SendIdentityRecoveryNotice,
        _ => throw new ArgumentOutOfRangeException(nameof(reactor)),
    };
}
