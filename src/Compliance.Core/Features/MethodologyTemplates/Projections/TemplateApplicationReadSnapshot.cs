using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projections;

/// <summary>An exact source-backed application read, separate from the compact mutation owner.</summary>
public sealed record TemplateApplicationReadSnapshot(TemplateApplication Owner, TemplateApplicationView? View);
