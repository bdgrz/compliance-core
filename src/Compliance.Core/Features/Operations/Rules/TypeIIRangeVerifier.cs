using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
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
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Rules;

/// <summary>
///     Verifies a receiver's frozen range intent: it is read from verified immutable content and the accepted opening
///     or amendment carrying exactly that intent is found on the authoritative period owner. Nothing a command states
///     is trusted until the owner confirms it; a fact that is not visible yet is transient.
/// </summary>
sealed class TypeIIRangeVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<TypeIIRangeChange> VerifyAsync(string realm, ImmutableContentRef intentReference,
        Uuid operationId, Uuid periodId, Uuid selectedCommandId, CancellationToken ct, Uuid selectedSourceId = default)
    {
        ArgumentNullException.ThrowIfNull(intentReference);
        Uuid tenantId;
        try
        {
            tenantId = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return TypeIIRangeChange.Failed(Permanent("The receiver names a stream outside a tenant realm."));
        }

        TypeIIRangeIntent intent;
        try
        {
            intent = await TypeIIRangeContent.ReadIntentAsync(content, realm, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return TypeIIRangeChange.Failed(new RequestError(RequestErrorKind.Conflict, "The frozen range intent cannot be verified.", exception is ImmutableContentUnavailableException));
        }

        if (operationId == Uuid.Empty || periodId == Uuid.Empty || intent.Header.RootOperationId != operationId ||
            intent.PeriodId != periodId || !intent.NewRange.IsValid ||
            intent.OldRange is { IsValid: false } ||
            (intent.OperationKind == TypeIIRangeOperationKind.Opening
                ? intent.PriorGrant is not null || intent.OldRange is not null
                : intent.PriorGrant is null || intent.OldRange is null))
            return TypeIIRangeChange.Failed(Permanent("The command differs from the frozen range intent."));

        var period = await reader.HydrateAsync(new TypeIIPeriod(tenantId, intent.ProgramId, intent.PeriodId,
            selectedCommandId, operationId, selectedSourceId), ct).ConfigureAwait(false);
        if (period.ProgramId != intent.ProgramId)
            return TypeIIRangeChange.Failed(Permanent("The frozen range intent belongs to another program owner."));
        if (period.AcceptedFor(operationId) is not { } accepted)
            return TypeIIRangeChange.Failed(Transient("The accepted opening or amendment is not visible yet."));
        if (TypeIIPeriod.IntentOf(accepted) != intentReference ||
            (accepted is TypeIIPeriodOpeningAccepted) != (intent.OperationKind == TypeIIRangeOperationKind.Opening))
            return TypeIIRangeChange.Failed(Permanent("The source is not the accepted operation of this intent."));
        var revision = accepted switch
        {
            TypeIIPeriodOpeningAccepted opening => opening.Revision,
            TypeIIPeriodAmendmentAccepted amendment => amendment.Revision,
            _ => throw new InvalidOperationException("An accepted operation is an opening or an amendment."),
        };
        return new TypeIIRangeChange(tenantId, period, accepted, revision, intent, null);
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
