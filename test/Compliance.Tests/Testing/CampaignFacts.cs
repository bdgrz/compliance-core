using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Builds campaign and participant facts for projection and replay tests that do not need a running owner. The
///     facts that projections read through content (the frozen audience, amendments and teams) are written to the
///     given content store; the rest carry opaque references because nothing reads them.
/// </summary>
static class CampaignFacts
{
    public static ImmutableContentRef Opaque(string schema = "test.v1") =>
        new(Uuid.CreateVersion4(), new string('a', 64), 1, "application/json", schema);

    public static OperationProvenance Provenance(Uuid logicalId, string kind, EventStreamAddress owner,
        ActorReference actor, DateTimeOffset at) => new(logicalId, logicalId, null, kind, actor, owner, "initial", null,
        new string('0', 64), at, owner, owner);

    public static async Task<PolicyCampaignLaunched> LaunchedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid campaignId, CampaignSubject subject, string audienceKind, IReadOnlyList<string> teams,
        DateOnly launchedOn, DateOnly dueOn, string? instructions, int audienceCount, ActorReference actor,
        Uuid actorMemberId, DateTimeOffset at)
    {
        var teamsRef = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.TeamsSchema,
            teams, CancellationToken.None);
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        return new PolicyCampaignLaunched(tenantId, programId, campaignId, subject, audienceKind, teamsRef,
            Uuid.CreateVersion4(), new string('b', 64), launchedOn, dueOn, instructions,
            subject.Kind == "policy" ? PolicyDistributionCampaign.DefaultAcknowledgementText : null, audienceCount,
            actor, actorMemberId, at, Provenance(campaignId, "LaunchPolicyCampaign", stream, actor, at), Opaque(),
            Opaque());
    }

    public static async Task<PolicyCampaignAudienceFrozen> FrozenAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid campaignId, params CampaignAudienceEntry[] entries)
    {
        var entriesRef = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.EntriesSchema,
            (IReadOnlyList<CampaignAudienceEntry>)entries, CancellationToken.None);
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        return new PolicyCampaignAudienceFrozen(tenantId, campaignId, 1, entriesRef,
            Provenance(Uuid.CreateVersion4(), "LaunchPolicyCampaign", stream, ActorReference.ForSystemProcess("test", "Test"), DateTimeOffset.UnixEpoch),
            null, null);
    }

    public static async Task<PolicyCampaignAudienceAmended> AmendedAsync(IImmutableContentStore content,
        Uuid tenantId, Uuid campaignId, long reconciliation, DateOnly observedOn, ActorReference actor,
        DateTimeOffset at, params CampaignAudienceAmendment[] amendments)
    {
        var manifest = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.AmendmentsSchema,
            (IReadOnlyList<CampaignAudienceAmendment>)amendments, CancellationToken.None);
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        var commandId = Uuid.CreateVersion4();
        return new PolicyCampaignAudienceAmended(tenantId, campaignId, reconciliation, 1, Uuid.CreateVersion4(),
            new string('c', 64), observedOn, manifest, actor, at,
            Provenance(commandId, "ReconcileCampaignAudience", stream, actor, at), Opaque(), Opaque());
    }

    public static PolicyAcknowledgementRecorded Acknowledged(Uuid tenantId, Uuid campaignId, Uuid personId,
        CampaignSubject subject, ActorReference performer, ActorReference recorder, bool onBehalf, DateTimeOffset at)
    {
        var stream = PolicyCampaignParticipant.StreamOf(tenantId, campaignId, personId);
        var id = Uuid.CreateVersion4();
        return new PolicyAcknowledgementRecorded(tenantId, campaignId, id, personId, subject.RecordId, subject.Version,
            subject.ContentSha256, PolicyDistributionCampaign.DefaultAcknowledgementText, performer, recorder,
            onBehalf, at, Provenance(id, "ApplyPolicyParticipantResult", stream, recorder, at), Opaque(), null,
            Opaque());
    }

    public static TrainingCompletionRecorded Completed(Uuid tenantId, Uuid campaignId, Uuid personId,
        CampaignSubject subject, DateOnly completedOn, ActorReference recorder, DateTimeOffset at)
    {
        var stream = PolicyCampaignParticipant.StreamOf(tenantId, campaignId, personId);
        var id = Uuid.CreateVersion4();
        return new TrainingCompletionRecorded(tenantId, campaignId, id, personId, subject.RecordId, subject.Version,
            completedOn, "manual", "evidence:completion", recorder, at,
            Provenance(id, "ApplyPolicyParticipantResult", stream, recorder, at), Opaque(), null, Opaque());
    }

    public static CampaignWaiverApproved Waived(Uuid tenantId, Uuid campaignId, Uuid personId, string reason,
        DateOnly expiresOn, ActorReference approver, Uuid approverMemberId, DateTimeOffset at)
    {
        var stream = PolicyCampaignParticipant.StreamOf(tenantId, campaignId, personId);
        var id = Uuid.CreateVersion4();
        return new CampaignWaiverApproved(tenantId, campaignId, id, personId, reason, expiresOn, approver,
            approverMemberId, at, Provenance(id, "ApplyPolicyParticipantResult", stream, approver, at), Opaque(), null,
            Opaque());
    }

    public static PolicyCampaignClosed Closed(Uuid tenantId, Uuid campaignId, DateOnly closedOn, string rationale,
        ActorReference actor, DateTimeOffset at)
    {
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        var id = Uuid.CreateVersion4();
        return new PolicyCampaignClosed(tenantId, campaignId, closedOn, rationale, Opaque(), actor, at,
            Provenance(id, "CompletePolicyCampaignClosure", stream, actor, at), 0, Opaque(), Opaque(), null, Opaque());
    }
}
