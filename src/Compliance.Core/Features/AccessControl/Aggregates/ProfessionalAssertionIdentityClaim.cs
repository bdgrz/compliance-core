using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
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
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     The permanent global claim of one signed assertion identity. The claim binds the complete verified tuple (the
///     frozen change intent it points to) and is never released, so an assertion identity cannot be recycled for a
///     different digest, designation, subject, duty or scope.
/// </summary>
public sealed class ProfessionalAssertionIdentityClaim : Aggregate
{
    /// <summary>Stream area of every assertion claim.</summary>
    public const string Area = "professional-assertion-identity-claims";

    ProfessionalAssertionIdentityClaimed? _claimed;

    /// <summary>Opens the claim of one assertion, selecting the logical operation to resolve.</summary>
    public ProfessionalAssertionIdentityClaim(Uuid assertionId, Uuid selectedLogicalId = default)
        : base(AggregateIdentity.Create(nameof(ProfessionalAssertionIdentityClaim), assertionId),
            new EventStreamAddress(ProfessionalDutyContent.Realm, Area, assertionId.ToString()))
    {
        AssertionId = assertionId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ProfessionalAssertionIdentityClaimed>(claimed =>
        {
            Acceptance.Observe(claimed);
            if (_claimed is not null || claimed.Owner != Stream || claimed.Operation.Owner != Stream ||
                claimed.Operation.RootOperationId != AssertionId || claimed.Revision != 1)
                throw new InvalidOperationException("A signed assertion identity is claimed exactly once, permanently.");
            _claimed = claimed;
        });
    }

    /// <summary>The assertion identity this claim owns.</summary>
    public Uuid AssertionId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True once a change claimed the assertion.</summary>
    public bool IsClaimed => _claimed is not null;

    /// <summary>The claim fact, once recorded.</summary>
    public ProfessionalAssertionIdentityClaimed? Claimed => _claimed;

    /// <summary>Claims the assertion for the change of <paramref name="operation" />.</summary>
    public Result Claim(OperationProvenance operation, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.RootOperationId != AssertionId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A claim is recorded on its own assertion stream."));
        if (_claimed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The signed assertion identity is permanently claimed.", false));
        RaiseEvent(new ProfessionalAssertionIdentityClaimed(Stream, 1, ProfessionalDutyEffects.Generation, intent,
            null, operation, null, null));
        return Result.Success;
    }
}
