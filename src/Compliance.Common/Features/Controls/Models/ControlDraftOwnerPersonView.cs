using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>The person-owner designation of the exact open draft version and revision.</summary>
public sealed record ControlDraftOwnerPersonView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid? DraftVersionId, long Revision, ControlPersonOwnerDesignationView? Designation);
