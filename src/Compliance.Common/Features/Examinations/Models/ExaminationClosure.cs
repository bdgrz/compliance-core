using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>The exact committed sign-off that first closed this examination.</summary>
public sealed record ExaminationClosure(Uuid SignOffId, Uuid SignOffEventId, Uuid OutcomeId);
