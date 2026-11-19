using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Queries;

/// <summary>Reads the highest ratified rule-set version.</summary>
[Discriminator("bdgrz.firm-services.rule-set.active", 1)]
public sealed record GetActiveIndependenceRuleSet() : IRequest<IndependenceRuleSetView>, ICallable;
