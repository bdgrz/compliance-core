using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class IndependenceRuleRatificationOperations(NativeOperationExecutor operations, IndependenceRuleRatificationReader ratifications,
    IndependenceRuleCatalogReader rules, ProfessionalDutyAuthorityReader duties, IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result<IndependenceRuleRatificationView>> RatifyAsync(IRequestContext<RatifyIndependenceRuleVersion> context,
        CurrentProfessionalDuty currentDuty, ActorReference actor, CancellationToken ct)
    {
        var request = context.Request;
        var snapshot = new IndependenceRuleRatificationCatalog();
        var operation = OwnerProvenance.For(snapshot, request.CommandId, nameof(RatifyIndependenceRuleVersion), actor, snapshot.Id.ToString(),
            unchecked((ulong)request.ExpectedRatificationSequence), AuthoredIntentDigest.Compute(request), clock.GetUtcNow()) with
        { FirstOwner = snapshot.Stream };
        IndependenceRatificationContent? prepared = null;
        ImmutableContentRef? body = null;
        ImmutableContentRef? result = null;
        IReadOnlyList<IndependenceRatificationHistoryRead> history = [];
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<IndependenceRuleRatificationCatalog>(AcceptedOperationBinding.Of(operation),
                () => new IndependenceRuleRatificationCatalog(request.CommandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        if (request.ExpectedRevision != request.ExpectedRatificationSequence)
                            return Refuse(RequestErrorKind.Validation, "A new ratification must author the current owner revision and ratification sequence consistently.");
                        if (owner.Uses.HasActiveUse)
                            return Refuse(RequestErrorKind.Conflict, "An engagement personal decision holds the ratification catalog; retry after it settles.", true);
                        var drafts = await rules.ReadAsync(token).ConfigureAwait(false);
                        if (drafts.Sequence != request.ExpectedRuleCatalogSequence || drafts.Current is not { } version || version.Version != request.RuleVersion ||
                            IndependenceSourceDigest.RuleContent(version.Content) != request.ExpectedContentDigest)
                            return Refuse(RequestErrorKind.Conflict, "The exact current draft changed; reload its immutable version and digest before ratifying.", true);
                        if (owner.Sequence != request.ExpectedRatificationSequence || request.RuleVersion <= (owner.Header?.RuleVersion ?? 0))
                            return Refuse(RequestErrorKind.Conflict, "The ratification sequence changed or a newer version is already active; reload before ratifying.", true);
                        var current = await ratifications.ReadAsync(owner, token).ConfigureAwait(false);
                        history = await ratifications.IdentityEvidenceAsync(current, request.RatificationId, token).ConfigureAwait(false);
                        if (!IndependenceCatalogHistory.ProvesAbsent(owner.CurrentContent, owner.Sequence, history, request.RatificationId))
                            return Refuse(RequestErrorKind.Conflict, "The ratification identity already names another personal decision.");
                        var confirmedDuty = await duties.ReadCurrentAsync(currentDuty.Staff.UserId, FirmProfessionalDuty.RuleRatifier, null, token).ConfigureAwait(false);
                        if (confirmedDuty is null || confirmedDuty.Staff.StaffMemberId != currentDuty.Staff.StaffMemberId || confirmedDuty.Staff.Revision != currentDuty.Staff.Revision ||
                            confirmedDuty.Designation.DesignationId != currentDuty.Designation.DesignationId || confirmedDuty.Designation.Revision != currentDuty.Designation.Revision)
                            return Refuse(RequestErrorKind.Conflict, "The rule-ratifier authority changed during this request; reload before ratifying.", true);
                        var pure = current.Rules;
                        var decision = pure.Ratify(request.CommandId, request.RatificationId, request.RuleVersion, request.ExpectedRuleCatalogSequence,
                            request.ExpectedRatificationSequence, request.ExpectedContentDigest, request.SourceReference, confirmedDuty.Staff,
                            confirmedDuty.Designation, actor, operation.AcceptedAt);
                        if (!decision.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(decision.Error!);
                        prepared = new(owner.Stream, request.CommandId, pure.Sequence, decision.Value, owner.CurrentContent);
                        body = await content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(), IndependenceCatalogContent.RatificationSchema,
                            prepared, token).ConfigureAwait(false);
                        var frozen = await IndependenceCatalogContent.FreezeResultAsync(content, IndependenceCatalogContent.RatificationResultSchema,
                            decision.Value, token).ConfigureAwait(false);
                        result = frozen.Result;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(frozen.Response);
                    }
                    catch (ImmutableContentIntegrityException error) { return Result<FirstResponseDescriptor>.Failure(IndependenceCatalogContent.Failure(error)); }
                }, (owner, first) => owner.Append(new IndependenceRuleRatificationRecorded(IndependenceCatalogContent.Header(prepared!), body!, sequence + 1,
                    operation, first, result!), prepared!, history, sequence, position)), context, ct).ConfigureAwait(false);
            return await IndependenceCatalogContent.ResponseAsync<IndependenceRuleRatificationView>(content, accepted,
                IndependenceCatalogContent.RatificationResultSchema, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error) { return Result<IndependenceRuleRatificationView>.Failure(IndependenceCatalogContent.Failure(error)); }
    }
    static Result<FirstResponseDescriptor> Refuse(RequestErrorKind kind, string message, bool transient = false) =>
        Result<FirstResponseDescriptor>.Failure(new RequestError(kind, message, transient));
}
