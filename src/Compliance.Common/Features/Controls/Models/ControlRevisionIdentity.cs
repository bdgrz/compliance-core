using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>One exact immutable content-revision discovery identity.</summary>
public sealed record ControlRevisionIdentity(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, ImmutableContentRef Content, ImmutableContentRef Change);
