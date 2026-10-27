using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyRegistration(Uuid PolicyId, string Identifier, long Revision);
