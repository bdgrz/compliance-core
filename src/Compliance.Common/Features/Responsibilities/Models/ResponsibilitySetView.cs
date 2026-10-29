using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Responsibilities.Models;

public sealed record ResponsibilitySetView(Uuid TenantId, Uuid SetId, ResponsibilityScope Scope, long Revision,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments);
