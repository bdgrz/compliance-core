using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
