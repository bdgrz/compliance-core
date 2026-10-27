using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record AccessGrantSetView(Uuid TenantId, long Revision, IReadOnlyList<AccessGrantView> Grants);
