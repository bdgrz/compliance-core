using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>One exact immutable content-revision discovery identity.</summary>
public sealed record ControlRevisionIdentity(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, ImmutableContentRef Content, ImmutableContentRef Change);
