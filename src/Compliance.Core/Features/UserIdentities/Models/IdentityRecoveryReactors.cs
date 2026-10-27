using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Reactors;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

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
