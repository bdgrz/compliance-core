using Bdgrz.Compliance.Features.Criteria.Handlers;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Bdgrz.Compliance.Features.Criteria.Projectors;
using Bdgrz.Compliance.Features.Criteria.Reactors;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Composition for independently retried criteria signals and the runtime index.</summary>
/// <remarks>
/// Registration does not start a worker. The worker host runs global Portia workloads and
/// ProcessSweep with durable process/content stores and system actor dispatch. Each signal is
/// persisted separately so that one unresolved operation does not hold the shared intake cursor.
/// </remarks>
public static class CriteriaWorkerRegistrationExtensions
{
    static readonly object RegistrationIdentity = new();

    /// <summary>Registers the criteria worker components without activating a host.</summary>
    public static PortiaBuilder AddCriteriaWorkers(this PortiaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddGeneratedRegistrations(RegistrationIdentity, static portia =>
        {
            var services = portia.Services;
            services.TryAddScoped<CriteriaLabelSignalIntake>();
            services.TryAddScoped<CriteriaEntrySignalIntake>();
            services.TryAddScoped<CriteriaValueSignalIntake>();
            services.TryAddScoped<CriteriaPublicationSignalIntake>();
            services.TryAddScoped<CriteriaLabelSourceVerifier>();
            services.TryAddScoped<CriteriaLabelRefusalVerifier>();
            services.TryAddScoped<ICriteriaLabelRefusalCommitter, CriteriaLabelRefusalCommitter>();
            services.TryAddScoped<CriteriaWitnessRefusalCommitter>();
            services.AddSingleton(CriteriaLabelSignalProcess.Definition());
            services.AddSingleton(CriteriaEntrySignalProcess.Definition());
            services.AddSingleton(CriteriaValueSignalProcess.Definition());
            services.AddSingleton(CriteriaPublicationSignalProcess.Definition());

            portia.AddRequestAuthorizer<CriteriaSignalSystemAuthorizer>()
                .AddRequestHandler<ReconcileCriteriaLabelSignalHandler>()
                .AddRequestHandler<ReconcileCriteriaEntrySignalHandler>()
                .AddRequestHandler<ReconcileCriteriaValueSignalHandler>()
                .AddRequestHandler<ReconcileCriteriaPublicationSignalHandler>()
                .AddRequestHandler<ReserveCriteriaEditionLabelHandler>()
                .AddRequestHandler<ApplyCriteriaLabelReservationHandler>()
                .AddRequestHandler<ConfirmCriteriaEditionLabelHandler>()
                .AddRequestHandler<ApplyCriteriaLabelConfirmationHandler>()
                .AddRequestHandler<ApplyCriteriaEditionLabelRefusalHandler>()
                .AddRequestHandler<DecideCriteriaEditionLabelRefusalHandler>()
                .AddRequestHandler<FenceCriteriaEditionLabelRequestHandler>()
                .AddRequestHandler<ReleaseCriteriaEditionLabelHandler>()
                .AddRequestHandler<PinCriteriaPublicationWitnessHandler>()
                .AddRequestHandler<CompleteCriteriaPublicationHandler>()
                .AddRequestHandler<AbortCriteriaPublicationHandler>()
                .AddRequestHandler<FenceCriteriaPublicationWitnessHandler>()
                .AddRequestHandler<DecideCriteriaWitnessRefusalHandler>()
                .AddRequestHandler<CompleteCriteriaPublicationAbortHandler>()
                .AddReactor<CriteriaLabelSignalReactor>("CriteriaLabelSignalV1", WorkloadScope.Global,
                    options => options.FailureAttemptLimit = int.MaxValue)
                .AddReactor<CriteriaEntrySignalReactor>("CriteriaEntrySignalV1", WorkloadScope.Global,
                    options => options.FailureAttemptLimit = int.MaxValue)
                .AddReactor<CriteriaValueSignalReactor>("CriteriaValueSignalV1", WorkloadScope.Global,
                    options => options.FailureAttemptLimit = int.MaxValue)
                .AddReactor<CriteriaPublicationSignalReactor>("CriteriaPublicationSignalV1", WorkloadScope.Global,
                    options => options.FailureAttemptLimit = int.MaxValue)
                .AddProjector<RuntimeCriteriaCatalogProjector>(RuntimeCriteriaCatalogProjector.WorkloadName,
                    WorkloadScope.Global);
        });
    }
}
