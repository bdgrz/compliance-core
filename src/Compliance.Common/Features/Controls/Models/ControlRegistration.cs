using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlRegistration(Uuid ControlId, string Identifier, long Revision);
