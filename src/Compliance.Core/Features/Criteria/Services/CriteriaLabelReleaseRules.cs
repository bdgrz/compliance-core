using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies the immutable facts authorizing one exact label release.</summary>
static class CriteriaLabelReleaseRules
{
    internal readonly record struct Authorization(string Reason, Uuid ReplacementConfirmationEventId);

    /// <summary>Validates a terminal operation fence without inferring that a reservation exists.</summary>
    internal static string? AuthorizeFence(Uuid editionId, Uuid operationId, string label,
        DomainEvent intent, DomainEvent terminal)
    {
        if (editionId == Uuid.Empty || operationId == Uuid.Empty || string.IsNullOrWhiteSpace(label) ||
            intent.Metadata.EventId == Uuid.Empty || terminal.Metadata.EventId == Uuid.Empty ||
            intent.Metadata.AggregateVersion >= terminal.Metadata.AggregateVersion)
            return null;
        var kind = intent switch
        {
            CriteriaEditionDraftRequested draft when draft.EditionId == editionId && draft.Framework == "tsc" &&
                draft.OperationId == operationId && draft.Label == label => "initial",
            CriteriaInitialLabelRequested initial when initial.EditionId == editionId &&
                initial.OperationId == operationId && initial.Label == label => "initial",
            CriteriaEditionRenameRequested rename when rename.EditionId == editionId &&
                rename.OperationId == operationId && rename.NewLabel == label => "rename",
            _ => null,
        };
        return terminal switch
        {
            CriteriaEditionDraftRequestCancelled cancelled when kind == "initial" &&
                cancelled.EditionId == editionId && cancelled.TargetOperationId == operationId &&
                cancelled.OperationId != Uuid.Empty && cancelled.OperationId != operationId &&
                cancelled.ExecutionGeneration > 0 => "cancelled",
            CriteriaEditionRenameCancelled cancelled when kind == "rename" &&
                cancelled.EditionId == editionId && cancelled.RenameOperationId == operationId &&
                cancelled.OperationId != Uuid.Empty && cancelled.OperationId != operationId &&
                cancelled.ExecutionGeneration > 0 => "cancelled",
            CriteriaInitialLabelRefused refused when kind == "initial" && refused.EditionId == editionId &&
                refused.OperationId == operationId && refused.Label == label && refused.RefusalEventId != Uuid.Empty &&
                !string.IsNullOrWhiteSpace(refused.Reason) => "refused",
            CriteriaEditionRenameRefused refused when kind == "rename" && refused.EditionId == editionId &&
                refused.OperationId == operationId && refused.RefusalEventId != Uuid.Empty &&
                !string.IsNullOrWhiteSpace(refused.Reason) => "refused",
            _ => null,
        };
    }

    internal static Authorization? Authorize(EditionLabelReserved reservation, DomainEvent intent,
        DomainEvent terminal, CriteriaEditionDraftRenamed? rename = null,
        EditionLabelConfirmed? replacementConfirmation = null,
        EditionLabelReserved? replacementReservation = null, CriteriaEditionRenameRequested? renameIntent = null)
    {
        if (reservation.Metadata.EventId == Uuid.Empty || reservation.EditionId == Uuid.Empty ||
            reservation.OperationId == Uuid.Empty || reservation.Generation <= 0 ||
            reservation.EditionRequestEventId == Uuid.Empty ||
            intent.Metadata.EventId != reservation.EditionRequestEventId || terminal.Metadata.EventId == Uuid.Empty ||
            intent.Metadata.AggregateVersion >= terminal.Metadata.AggregateVersion ||
            !MatchesIntent(reservation, intent))
            return null;

        return terminal switch
        {
            CriteriaEditionDraftRequestCancelled cancelled when
                intent is CriteriaEditionDraftRequested or CriteriaInitialLabelRequested &&
                cancelled.EditionId == reservation.EditionId && cancelled.TargetOperationId == reservation.OperationId &&
                cancelled.OperationId != Uuid.Empty && cancelled.OperationId != cancelled.TargetOperationId &&
                cancelled.ExecutionGeneration > 0 =>
                new("cancelled", Uuid.Empty),
            CriteriaEditionRenameCancelled cancelled when intent is CriteriaEditionRenameRequested &&
                cancelled.EditionId == reservation.EditionId && cancelled.RenameOperationId == reservation.OperationId &&
                cancelled.OperationId != Uuid.Empty && cancelled.OperationId != cancelled.RenameOperationId &&
                cancelled.ExecutionGeneration > 0 =>
                new("cancelled", Uuid.Empty),
            CriteriaInitialLabelRefused refused when
                intent is CriteriaEditionDraftRequested or CriteriaInitialLabelRequested &&
                refused.EditionId == reservation.EditionId && refused.OperationId == reservation.OperationId &&
                refused.Label == reservation.Label && refused.RefusalEventId != Uuid.Empty &&
                !string.IsNullOrWhiteSpace(refused.Reason) => new("refused", Uuid.Empty),
            CriteriaEditionRenameRefused refused when intent is CriteriaEditionRenameRequested &&
                refused.EditionId == reservation.EditionId && refused.OperationId == reservation.OperationId &&
                refused.RefusalEventId != Uuid.Empty && !string.IsNullOrWhiteSpace(refused.Reason) =>
                new("refused", Uuid.Empty),
            CriteriaEditionDraftAbandoned abandoned when abandoned.EditionId == reservation.EditionId &&
                abandoned.Label == reservation.Label && abandoned.LabelGeneration == reservation.Generation &&
                abandoned.OperationId != Uuid.Empty && abandoned.DraftGeneration != Uuid.Empty &&
                abandoned.ExecutionGeneration > 0 =>
                new("abandoned", Uuid.Empty),
            CriteriaEditionRenameCompleted completed when MatchesReplacement(reservation, completed, rename,
                replacementConfirmation, replacementReservation, renameIntent) =>
                new("renamed", completed.ConfirmationEventId),
            _ => null,
        };
    }

    static bool MatchesIntent(EditionLabelReserved reservation, DomainEvent intent) => intent switch
    {
        CriteriaEditionDraftRequested draft => draft.EditionId == reservation.EditionId && draft.Framework == "tsc" &&
            draft.OperationId == reservation.OperationId && draft.Label == reservation.Label &&
            draft.Actor == reservation.RequestedBy,
        CriteriaInitialLabelRequested initial => initial.EditionId == reservation.EditionId &&
            initial.OperationId == reservation.OperationId && initial.Label == reservation.Label &&
            initial.Actor == reservation.RequestedBy,
        CriteriaEditionRenameRequested request => request.EditionId == reservation.EditionId &&
            request.OperationId == reservation.OperationId && request.NewLabel == reservation.Label &&
            request.Actor == reservation.RequestedBy,
        _ => false,
    };

    static bool MatchesReplacement(EditionLabelReserved old, CriteriaEditionRenameCompleted completed,
        CriteriaEditionDraftRenamed? rename, EditionLabelConfirmed? confirmation,
        EditionLabelReserved? reservation, CriteriaEditionRenameRequested? intent) =>
        rename is not null && confirmation is not null && reservation is not null && intent is not null &&
        rename.Metadata.EventId != Uuid.Empty &&
        confirmation.Metadata.EventId == completed.ConfirmationEventId &&
        reservation.Metadata.EventId == rename.ReservationEventId &&
        reservation.Metadata.AggregateVersion < confirmation.Metadata.AggregateVersion &&
        reservation.EditionRequestEventId == intent.Metadata.EventId &&
        MatchesIntent(reservation, intent) && intent.OldLabel == old.Label &&
        intent.OldLabelGeneration == old.Generation &&
        intent.Metadata.AggregateVersion < rename.Metadata.AggregateVersion &&
        rename.Metadata.AggregateVersion < completed.Metadata.AggregateVersion &&
        completed.EditionId == old.EditionId && rename.EditionId == old.EditionId &&
        rename.OperationId == completed.OperationId && rename.OldLabel == old.Label &&
        rename.OldLabelGeneration == old.Generation && rename.NewLabel == completed.Label &&
        rename.NewLabelGeneration == completed.LabelGeneration &&
        confirmation.EditionId == old.EditionId && confirmation.OperationId == completed.OperationId &&
        confirmation.Label == completed.Label && confirmation.Generation == completed.LabelGeneration &&
        reservation.EditionId == old.EditionId && reservation.OperationId == completed.OperationId &&
        reservation.Label == completed.Label && reservation.Generation == completed.LabelGeneration &&
        confirmation.EditionAcceptedEventId == rename.Metadata.EventId;
}
