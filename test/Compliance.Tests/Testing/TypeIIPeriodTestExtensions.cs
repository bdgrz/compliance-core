using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Applies an opening or amendment straight onto a period aggregate, accepted and applied in one step with a
///     synthetic calendar permit, exactly as the range process leaves them. It is a test seed for the pure aggregate
///     rules; the production path always runs admission, calendar acquisition and apply as separate facts.
/// </summary>
static class TypeIIPeriodTestExtensions
{
    static readonly ImmutableContentRef Synthetic = new(Uuid.Parse("00000000-0000-4000-8000-0000000000f3",
        System.Globalization.CultureInfo.InvariantCulture), new string('d', 64), 2, "application/json",
        "synthetic-content.v1");

    /// <summary>Opens the period; returns the same failure the aggregate reports for an invalid opening.</summary>
    public static CommandFailure? Start(this TypeIIPeriodRules period, TypeIIPlanView plan,
        TypeIIPeriodOpeningContent opening, Uuid actorMemberId, string actorDisplay, DateTimeOffset at,
        ImmutableContentRef? intent = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        var failure = period.PrepareOpening(plan, opening, actorMemberId, actorDisplay, at, out var initial);
        if (failure is not null)
            return failure;
        var accepted = period.AcceptOpening(Provenance(period, period.Id, period.Id, nameof(Start), actorMemberId,
            actorDisplay, at), intent ?? Synthetic, Synthetic);
        if (!accepted.IsSuccess)
            throw new InvalidOperationException(accepted.Error!.Message);
        var applied = period.ApplyOpening(Provenance(period, Uuid.CreateVersion4(), period.Id, nameof(Start),
            actorMemberId, actorDisplay, at), initial!, new TypeIIPeriodCalendarAuthorization(period.Id, 1), Synthetic);
        if (!applied.IsSuccess)
            throw new InvalidOperationException(applied.Error!.Message);
        return null;
    }

    /// <summary>Amends the period range; returns the same failure the aggregate reports for an invalid amendment.</summary>
    public static CommandFailure? Amend(this TypeIIPeriodRules period, long expectedRevision, DateOnly periodStart,
        DateOnly periodEnd, string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(period);
        var failure = period.PrepareAmendment(expectedRevision, periodStart, periodEnd, reason, actorMemberId,
            actorDisplay, at, out _);
        if (failure is not null)
            return failure;
        var amendmentId = Uuid.CreateVersion4();
        var accepted = period.AcceptAmendment(expectedRevision, Provenance(period, amendmentId, amendmentId,
            nameof(Amend), actorMemberId, actorDisplay, at), Synthetic, Synthetic);
        if (!accepted.IsSuccess)
            throw new InvalidOperationException(accepted.Error!.Message);
        var applied = period.ApplyAmendment(Provenance(period, Uuid.CreateVersion4(), amendmentId, nameof(Amend),
            actorMemberId, actorDisplay, at), new TypeIIPeriodAmendmentChange(periodStart, periodEnd, reason,
            actorMemberId, actorDisplay, at), new TypeIIPeriodCalendarAuthorization(amendmentId,
            period.CalendarAuthorization!.Generation + 1), Synthetic);
        if (!applied.IsSuccess)
            throw new InvalidOperationException(applied.Error!.Message);
        return null;
    }

    /// <summary>Selects the due occurrences of a generation key as the native operation of the period.</summary>
    public static Result Generate(this TypeIIPeriodRules period, string generationKey, DateOnly throughDate,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(period);
        var logicalId = TypeIIPeriod.GenerationLogicalId(period.Id, generationKey);
        return period.GenerateDueOccurrences(Provenance(period, logicalId, logicalId, "GenerateDueControlOccurrences",
            actorMemberId, actorDisplay, at), generationKey, throughDate, actorMemberId, actorDisplay, at, Synthetic,
            Synthetic, Synthetic, Synthetic);
    }

    /// <summary>Seeds the native close fact for pure owner-rule tests; the production handler derives/publishes its sources.</summary>
    public static CommandFailure? RecordCloseValidation(this TypeIIPeriodRules period, long expectedRevision,
        TypeIIPeriodCloseValidationContent content, Uuid validatorMemberId, string validatorDisplay, DateTimeOffset at)
    {
        var operation = RootOperation.Provenance(Uuid.CreateVersion4(), nameof(RecordTypeIIPeriodCloseValidation), period.Stream,
            ActorReference.ForMember(validatorMemberId, validatorDisplay), expectedRevision, new string('e', 64), at);
        return period.RecordCloseValidation(operation, content with { RecordedThrough = at }, Synthetic with { Schema = TypeIIPeriodCloseValidationChange.Schema },
            Synthetic with { Schema = FirstResponses.Schema }, Synthetic with { Schema = "mutation-result-content.v1" });
    }

    static OperationProvenance Provenance(TypeIIPeriodRules period, Uuid logicalId, Uuid rootId, string kind,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at) => new(logicalId, rootId,
        logicalId == rootId ? null : rootId, kind, ActorReference.ForMember(actorMemberId, actorDisplay),
        period.Stream, "initial", null, new string('e', 64), at, period.Stream, period.Stream);
}
