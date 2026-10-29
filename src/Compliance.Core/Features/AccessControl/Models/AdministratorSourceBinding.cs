using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The source revisions an admission pins into its durable intent after the source preflight passed against
///     the hydrated owners and current guard state.
/// </summary>
public sealed record AdministratorSourceBinding(ulong MemberRevision, ulong? AssignmentGeneration,
    ulong? ExpectedAssignmentRevision, string? Reason);
