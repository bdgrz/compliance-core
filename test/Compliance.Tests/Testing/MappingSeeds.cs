using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
            new ControlCriterionMappingRules(tenantId, programId, controlId, editionId, criterion), line =>
                line.Propose(MappingRuleFacts.Root(line, id, "ProposeControlCriterionMapping", expectedRevision,
                        actorMemberId, at), controlVersionId, kind, rationale, explanation, actorMemberId, display, at));
        return id;
    }

    /// <summary>Reviews the pending mapping proposal.</summary>
    public static Task ReviewMappingAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid editionId, string criterion, Uuid mappingId, long expectedRevision, string outcome, string rationale,
        Uuid reviewerMemberId, string display, DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        return ProgramManagementServices.SeedAsync(provider,
            new ControlCriterionMappingRules(tenantId, programId, controlId, editionId, criterion), line =>
                line.Review(MappingRuleFacts.Root(line, commandId, "ReviewControlCriterionMapping", expectedRevision,
                        reviewerMemberId, at), mappingId, outcome, rationale, reviewerMemberId, display, at, null));
    }

    /// <summary>Proposes a not-applicable decision on the criterion's line; the proposal id is returned.</summary>
    public static async Task<Uuid> ProposeNotApplicableAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid editionId, string criterion, long expectedRevision, string rationale, Uuid actorMemberId, string display,
        DateTimeOffset at, Uuid? decisionId = null)
    {
        var id = decisionId ?? Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider,
            new CriterionApplicabilityRules(tenantId, programId, editionId, criterion), line =>
                line.Propose(MappingRuleFacts.Root(line, id, "ProposeCriterionNotApplicable", expectedRevision,
                        actorMemberId, at), rationale, actorMemberId, ActorReference.ForMember(actorMemberId, display),
                    at));
        return id;
    }

    /// <summary>Reviews the pending not-applicable proposal.</summary>
    public static Task ReviewNotApplicableAsync(IServiceProvider provider, Uuid tenantId, Uuid programId,
        Uuid editionId, string criterion, Uuid decisionId, long expectedRevision, string outcome, string rationale,
        Uuid reviewerMemberId, string display, DateTimeOffset at)
    {
        var commandId = Uuid.CreateVersion4();
        return ProgramManagementServices.SeedAsync(provider,
            new CriterionApplicabilityRules(tenantId, programId, editionId, criterion), line =>
                line.Review(MappingRuleFacts.Root(line, commandId, "ReviewCriterionApplicability", expectedRevision,
                        reviewerMemberId, at), decisionId, outcome, rationale, reviewerMemberId,
                    ActorReference.ForMember(reviewerMemberId, display), at, null));
    }
}
