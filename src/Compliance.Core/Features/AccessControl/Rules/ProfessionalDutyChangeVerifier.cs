using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

/// <summary>
///     Verifies a receiver's proven source: the frozen change intent and verified assertion are read from verified
///     immutable content, and the accepted change fact is found on the authoritative designation owner. The source
///     reference only names the fact; nothing the caller states is trusted until the owner confirms it.
/// </summary>
sealed class ProfessionalDutyChangeVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<ProfessionalDutyChange> VerifyAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid assertionId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        ProfessionalDutyChangeIntent intent;
        VerifiedFirmProfessionalDutyAssertion evidence;
        try
        {
            intent = await ProfessionalDutyContent.ReadIntentAsync(content, intentReference, ct).ConfigureAwait(false);
            evidence = await ProfessionalDutyContent.ReadVerifiedScopeAsync(content, intent.VerifiedScope, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return ProfessionalDutyChange.Failed(exception is ImmutableContentUnavailableException
                ? Transient($"The frozen change is not available: {exception.Message}")
                : Permanent($"The frozen change cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != assertionId || intent.VerifiedAssertionId != assertionId ||
            evidence.AssertionId != assertionId || evidence.DesignationId != intent.DesignationId ||
            evidence.Status != (intent.ChangeKind == ProfessionalDutyChangeKind.Record ? "active" : "revoked") ||
            ProfessionalDutyRules.EvidenceDigest(intent.SignedAssertion) != evidence.EvidenceDigest ||
            intent.ChangeKind == ProfessionalDutyChangeKind.Record != (intent.StaffEligibility is not null))
            return ProfessionalDutyChange.Failed(Permanent("The command differs from the frozen change intent."));

        var owner = await reader.HydrateAsync(
            new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty, evidence.TenantId, commandId, assertionId), ct)
            .ConfigureAwait(false);
        if (owner.AcceptedFor(assertionId) is not { } accepted)
            return ProfessionalDutyChange.Failed(Transient("The accepted change is not visible yet."));
        if (source.Stream != owner.Stream || accepted.Metadata.EventId != source.EventId ||
            accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference)
            return ProfessionalDutyChange.Failed(Permanent("The source is not the accepted change of this designation."));
        try
        {
            ProfessionalDutyContent.RequireIntent(owner, accepted, intent, evidence);
            await ProfessionalDutyContent.RequireAuthoredAsync(content, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return ProfessionalDutyChange.Failed(error is ImmutableContentUnavailableException ? Transient(error.Message) : Permanent(error.Message));
        }
        return new ProfessionalDutyChange(owner, accepted, intent, evidence, null);
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
