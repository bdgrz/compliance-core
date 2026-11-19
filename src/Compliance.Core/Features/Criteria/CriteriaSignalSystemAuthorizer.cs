using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Allows only the trusted worker to dispatch criteria process participant commands.</summary>
sealed class CriteriaSignalSystemAuthorizer :
    IRequestAuthorizer<ReconcileCriteriaLabelSignal>,
    IRequestAuthorizer<ReconcileCriteriaEntrySignal>,
    IRequestAuthorizer<ReconcileCriteriaValueSignal>,
    IRequestAuthorizer<ReconcileCriteriaPublicationSignal>,
    IRequestAuthorizer<ReserveCriteriaEditionLabel>,
    IRequestAuthorizer<ApplyCriteriaLabelReservation>,
    IRequestAuthorizer<ConfirmCriteriaEditionLabel>,
    IRequestAuthorizer<ApplyCriteriaLabelConfirmation>,
    IRequestAuthorizer<ApplyCriteriaEditionLabelRefusal>,
    IRequestAuthorizer<DecideCriteriaEditionLabelRefusal>,
    IRequestAuthorizer<FenceCriteriaEditionLabelRequest>,
    IRequestAuthorizer<ReleaseCriteriaEditionLabel>,
    IRequestAuthorizer<PinCriteriaPublicationWitness>,
    IRequestAuthorizer<CompleteCriteriaPublication>,
    IRequestAuthorizer<AbortCriteriaPublication>,
    IRequestAuthorizer<FenceCriteriaPublicationWitness>,
    IRequestAuthorizer<DecideCriteriaWitnessRefusal>,
    IRequestAuthorizer<CompleteCriteriaPublicationAbort>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReconcileCriteriaLabelSignal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReconcileCriteriaEntrySignal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReconcileCriteriaValueSignal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReconcileCriteriaPublicationSignal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReserveCriteriaEditionLabel> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ApplyCriteriaLabelReservation> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ConfirmCriteriaEditionLabel> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ApplyCriteriaLabelConfirmation> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ApplyCriteriaEditionLabelRefusal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<DecideCriteriaEditionLabelRefusal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<FenceCriteriaEditionLabelRequest> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ReleaseCriteriaEditionLabel> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<PinCriteriaPublicationWitness> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<CompleteCriteriaPublication> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<AbortCriteriaPublication> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<FenceCriteriaPublicationWitness> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<DecideCriteriaWitnessRefusal> context, CancellationToken ct) => Authorize(context.Actor);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<CompleteCriteriaPublicationAbort> context, CancellationToken ct) => Authorize(context.Actor);

    static ValueTask<Result> Authorize(System.Security.Claims.ClaimsPrincipal actor) =>
        ValueTask.FromResult(RequestActor.IsSystem(actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the trusted system worker may dispatch criteria participant requests.")));
}

