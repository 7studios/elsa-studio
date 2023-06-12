using Elsa.Studio.Contracts;
using Elsa.Studio.Workflows.Designer.Contracts;
using Elsa.Studio.Workflows.Designer.Interop;
using Elsa.Studio.Workflows.Designer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Elsa.Studio.Workflows.Designer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowsDesigner(this IServiceCollection services)
    {
        return services
            .AddSingleton<IModule, Module>()
            .AddScoped<IFlowchartMapper, DefaultFlowchartMapper>()
            .AddScoped<DesignerJsInterop>();
    }
}