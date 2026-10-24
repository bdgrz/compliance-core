namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>The receiver names of the identity recovery process, as the process catalog spells them.</summary>
public static class IdentityRecoveryReceivers
{
    public const string MaterializeIdentityRecoveryPreparation = nameof(MaterializeIdentityRecoveryPreparation);
    public const string ProtectRecoveryIdentity = nameof(ProtectRecoveryIdentity);
    public const string AcceptProtectedIdentityRecovery = nameof(AcceptProtectedIdentityRecovery);
    public const string RegisterRecoveredIdentity = nameof(RegisterRecoveredIdentity);
    public const string RevokeRecoveredOldIdentity = nameof(RevokeRecoveredOldIdentity);
    public const string CompleteAcceptedIdentityRecovery = nameof(CompleteAcceptedIdentityRecovery);
    public const string SettleRecoveryIdentityProtection = nameof(SettleRecoveryIdentityProtection);
    public const string CompleteEmailRecoveryChallenge = nameof(CompleteEmailRecoveryChallenge);
    public const string SendIdentityRecoveryNotice = nameof(SendIdentityRecoveryNotice);
    public const string RejectIdentityRecoveryPreparation = nameof(RejectIdentityRecoveryPreparation);
}
