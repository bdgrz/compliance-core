using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Work.Aggregates;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>One visible item with the actor's relationship to it.</summary>
sealed record WorkQueueEntry(WorkCandidate Candidate, WorkAssignmentState State,
    WorkAssignmentState LegacyState, WorkQueueItemView Item, bool ActorEligible, bool ActorInTeam, WorkItemAssignment? NativeOwner = null);
