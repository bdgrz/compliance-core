using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

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
