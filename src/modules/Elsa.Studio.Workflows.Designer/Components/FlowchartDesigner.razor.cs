using System.Text.Json;
using System.Text.Json.Nodes;
using Elsa.Studio.Contracts;
using Elsa.Studio.Workflows.Core.Contracts;
using Elsa.Studio.Workflows.Designer.Contracts;
using Elsa.Studio.Workflows.Designer.Interop;
using Elsa.Studio.Workflows.Designer.Models;
using Elsa.Studio.Workflows.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;

namespace Elsa.Studio.Workflows.Designer.Components;

public partial class FlowchartDesigner : IDisposable, IAsyncDisposable
{
    private readonly string _containerId = $"container-{Guid.NewGuid():N}";
    private DotNetObjectReference<FlowchartDesigner>? _componentRef;
    private IFlowchartMapper? _flowchartMapper = default!;
    private X6GraphApi _graphApi = default!;
    
    private List<BreadcrumbItem> _activityPath = new()
    {
        new("Flowchart1", href: "#", icon: ActivityIcons.Flowchart),
        new("ForEach1", href: "#", icon: @Icons.Material.Outlined.RepeatOne),
    };

    [Parameter] public JsonElement Flowchart { get; set; }

    [Inject] private DesignerJsInterop DesignerJsInterop { get; set; } = default!;
    [Inject] private IThemeService ThemeService { get; set; } = default!;
    [Inject] private IActivityRegistry ActivityRegistry { get; set; } = default!;
    [Inject] private IFlowchartMapperFactory FlowchartMapperFactory { get; set; } = default!;
    
    [JSInvokable]
    public void OnActivitySelected(JsonElement activity)
    {
        StateHasChanged();
    }

    public async Task<JsonElement> ReadFlowchartAsync()
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var data = await _graphApi.ReadGraphAsync();
        var cells = data.GetProperty("cells").EnumerateArray();
        var nodes = cells.Where(x => x.GetProperty("shape").GetString() == "elsa-activity").Select(x => x.Deserialize<X6Node>(serializerOptions)!).ToList();
        var edges = cells.Where(x => x.GetProperty("shape").GetString() == "elsa-edge").Select(x => x.Deserialize<X6Edge>(serializerOptions)!).ToList();
        var graph = new X6Graph(nodes, edges);
        var flowchartMapper = await GetFlowchartMapperAsync();
        var graphJson = flowchartMapper.MapX6Graph(graph);
        var activities = graphJson.GetProperty("activities");
        var connections = graphJson.GetProperty("connections");

        var flowchart = JsonObject.Create(Flowchart)!;
        flowchart["activities"] = JsonArray.Create(activities);
        flowchart["connections"] = JsonArray.Create(connections);

        return JsonSerializer.SerializeToElement(flowchart);
    }

    public async Task AddActivityAsync(JsonElement activity)
    {
        var flowchartMapper = await GetFlowchartMapperAsync();
        var node = flowchartMapper.MapActivity(activity);
        await _graphApi.AddActivityNodeAsync(node);
    }

    public async Task ZoomToFitAsync() => await _graphApi.ZoomToFitAsync();
    public async Task CenterContentAsync() => await _graphApi.CenterContentAsync();

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _graphApi.DisposeGraphAsync();
    }

    public void Dispose()
    {
        ThemeService.IsDarkModeChanged -= OnDarkModeChanged;
        _componentRef?.Dispose();
    }

    protected override void OnInitialized()
    {
        ThemeService.IsDarkModeChanged += OnDarkModeChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _componentRef = DotNetObjectReference.Create(this);
            _graphApi = await DesignerJsInterop.CreateGraphAsync(_containerId, _componentRef);

            var flowchartMapper = await GetFlowchartMapperAsync();
            var graph = flowchartMapper.MapFlowchart(Flowchart);
            await _graphApi.LoadGraphAsync(graph);
        }
    }
    
    private async Task<IFlowchartMapper> GetFlowchartMapperAsync() => _flowchartMapper ??= await FlowchartMapperFactory.CreateAsync();

    /// <summary>
    /// Sets the grid color.
    /// </summary>
    private async Task SetGridColorAsync(string color) => await _graphApi.SetGridColorAsync(color);

    private async void OnDarkModeChanged()
    {
        var palette = ThemeService.CurrentPalette;
        var gridColor = palette.BackgroundGrey;
        await SetGridColorAsync(gridColor.ToString(MudColorOutputFormats.HexA));
    }
}