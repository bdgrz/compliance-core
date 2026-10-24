using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Seeds mapping and applicability lines directly on their owner streams, as a prior accepted operation.</summary>
static class MappingSeeds
{
    /// <summary>Proposes a mapping on the tuple's line; the proposal id is returned.</summary>
    public static async Task<Uuid> ProposeMappingAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid controlId, Uuid controlVersionId, Uuid editionId, string criterion, long expectedRevision, string rationale,
        string explanation, Uuid actorMemberId, string display, DateTimeOffset at, string kind = "criterion",
        Uuid? mappingId = null)
    {
        var id = mappingId ?? Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider,
            new ControlCriterionMapping(tenantId, programId, controlId, editionId, criterion, id), line =>
                line.Propose(NativeFacts.Root(line, id, "ProposeControlCriterionMapping", expectedRevision,
                        actorMemberId, at), controlVersionId, kind, rationale, explanation, actorMemberId, display, at,
                    NativeFacts.Ref(), NativeFacts.Ref()));
        return id;
    }

    /// <summary>Reviews the pending mapping proposal.</summary>
    public static Task ReviewMappingAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid editionId, string criterion, Uuid mappingId, long expectedRevision, string outcome, string rationale,
        Uuid reviewerMemberId, string display, DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        return ProgramManagementServices.SeedAsync(provider,
            new ControlCriterionMapping(tenantId, programId, controlId, editionId, criterion, commandId), line =>
                line.Review(NativeFacts.Root(line, commandId, "ReviewControlCriterionMapping", expectedRevision,
                        reviewerMemberId, at), mappingId, outcome, rationale, reviewerMemberId, display, at, null,
                    NativeFacts.Ref(), NativeFacts.Ref()));
    }

    /// <summary>Proposes a not-applicable decision on the criterion's line; the proposal id is returned.</summary>
    public static async Task<Uuid> ProposeNotApplicableAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid editionId, string criterion, long expectedRevision, string rationale, Uuid actorMemberId, string display,
        DateTimeOffset at, Uuid? decisionId = null)
    {
        var id = decisionId ?? Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider,
            new CriterionApplicability(tenantId, programId, editionId, criterion, id), line =>
                line.Propose(NativeFacts.Root(line, id, "ProposeCriterionNotApplicable", expectedRevision,
                        actorMemberId, at), rationale, actorMemberId, ActorReference.ForMember(actorMemberId, display),
                    at, NativeFacts.Ref(), NativeFacts.Ref()));
        return id;
    }

    /// <summary>Reviews the pending not-applicable proposal.</summary>
    public static Task ReviewNotApplicableAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid editionId, string criterion, Uuid decisionId, long expectedRevision, string outcome, string rationale,
        Uuid reviewerMemberId, string display, DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        return ProgramManagementServices.SeedAsync(provider,
            new CriterionApplicability(tenantId, programId, editionId, criterion, commandId), line =>
                line.Review(NativeFacts.Root(line, commandId, "ReviewCriterionApplicability", expectedRevision,
                        reviewerMemberId, at), decisionId, outcome, rationale, reviewerMemberId,
                    ActorReference.ForMember(reviewerMemberId, display), at, null, NativeFacts.Ref(),
                    NativeFacts.Ref()));
    }
}
