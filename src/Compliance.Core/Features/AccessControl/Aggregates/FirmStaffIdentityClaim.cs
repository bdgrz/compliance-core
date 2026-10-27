using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     The permanent claim of one canonical user by exactly one staff record. Deactivating a staff member never
///     releases the claim.
/// </summary>
public sealed class FirmStaffIdentityClaim : Aggregate
{
    /// <summary>Stream area of every canonical-user claim.</summary>
    public const string Area = "firm-staff-identity-claims";

    FirmStaffIdentityClaimed? _claimed;

    /// <summary>Opens the claim of one canonical user, selecting the logical operation to resolve.</summary>
    public FirmStaffIdentityClaim(Uuid userId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(FirmStaffIdentityClaim), userId),
            new EventStreamAddress(FirmStaffContent.Realm, Area, userId.ToString()))
    {
        UserId = userId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<FirmStaffIdentityClaimed>(claimed =>
        {
            Acceptance.Observe(claimed);
            if (_claimed is not null || claimed.Owner != Stream || claimed.Operation.Owner != Stream ||
                claimed.Operation.RootOperationId == Uuid.Empty || claimed.Revision != 1)
                throw new InvalidOperationException("A canonical user is claimed exactly once, permanently.");
            _claimed = claimed;
        });
    }

    /// <summary>The canonical user this claim owns.</summary>
    public Uuid UserId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True once a staff record claimed the user.</summary>
    public bool IsClaimed => _claimed is not null;

    /// <summary>The claim fact, once recorded.</summary>
    public FirmStaffIdentityClaimed? Claimed => _claimed;

    /// <summary>The staff member holding the claim: the root operation of the registration that claimed it.</summary>
    public Uuid? ClaimedByStaffMemberId => _claimed?.Operation.RootOperationId;

    /// <summary>Claims the user for the registration of <paramref name="operation" />.</summary>
    public Result Claim(OperationProvenance operation, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A claim is recorded on its own canonical user stream."));
        if (_claimed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The canonical user is permanently claimed.", false));
        RaiseEvent(new FirmStaffIdentityClaimed(Stream, 1, FirmStaffEffects.Generation, intent, null, operation,
            null, null));
        return Result.Success;
    }
}
