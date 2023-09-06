using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Elsa.Api.Client.Extensions;
using Elsa.Api.Client.Resources.ActivityDescriptors.Enums;
using Elsa.Api.Client.Resources.ActivityDescriptors.Models;
using Elsa.Studio.Contracts;
using Elsa.Studio.Workflows.Designer;
using Elsa.Studio.Workflows.Designer.Models;
using Elsa.Studio.Workflows.Domain.Contexts;
using Elsa.Studio.Workflows.Domain.Contracts;
using Elsa.Studio.Workflows.Extensions;
using Elsa.Studio.Workflows.Models;
using Elsa.Studio.Workflows.Shared.Args;
using Elsa.Studio.Workflows.UI.Args;
using Elsa.Studio.Workflows.UI.Contracts;
using Elsa.Studio.Workflows.UI.Models;
using Humanizer;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Elsa.Studio.Workflows.Shared.Components;

/// <summary>
/// A wrapper around the diagram designer that provides a breadcrumb and a toolbar.
/// </summary>
public partial class DiagramDesignerWrapper
{
    private IDiagramDesigner? _diagramDesigner;
    private Stack<ActivityPathSegment> _pathSegments = new();
    private List<BreadcrumbItem> _breadcrumbItems = new();
    private IDictionary<string, ActivityStats> _activityStats = new Dictionary<string, ActivityStats>();

    [Parameter] public JsonObject Activity { get; set; } = default!;
    [Parameter] public bool IsReadOnly { get; set; }
    [Parameter] public string? WorkflowInstanceId { get; set; }
    [Parameter] public RenderFragment CustomToolbarItems { get; set; } = default!;
    [Parameter] public bool IsProgressing { get; set; }
    [Parameter] public Func<JsonObject, Task>? ActivitySelected { get; set; }
    [Parameter] public Func<Task>? GraphUpdated { get; set; }
    [Parameter] public Func<DesignerPathChangedArgs, Task>? PathChanged { get; set; }
    [Inject] private IDiagramDesignerService DiagramDesignerService { get; set; } = default!;
    [Inject] private IActivityDisplaySettingsRegistry ActivityDisplaySettingsRegistry { get; set; } = default!;
    [Inject] private IActivityPortService ActivityPortService { get; set; } = default!;
    [Inject] private IActivityRegistry ActivityRegistry { get; set; } = default!;
    [Inject] private IActivityIdGenerator ActivityIdGenerator { get; set; } = default!;
    [Inject] private IActivityExecutionService ActivityExecutionService { get; set; } = default!;

    private ActivityPathSegment? CurrentPathSegment => _pathSegments.TryPeek(out var segment) ? segment : default;

    public Task<JsonObject> ReadActivityAsync()
    {
        return Task.FromResult(Activity);
    }

    public async Task LoadActivityAsync(JsonObject activity)
    {
        await _diagramDesigner!.LoadRootActivityAsync(activity, _activityStats);
        await UpdatePathSegmentsAsync(segments => segments.Clear());
    }

    public async Task UpdateActivityAsync(string activityId, JsonObject activity)
    {
        var currentContainer = GetCurrentContainerActivity();

        if (currentContainer == activity)
        {
            if (GraphUpdated != null)
                await GraphUpdated();

            return;
        }

        await _diagramDesigner!.UpdateActivityAsync(activityId, activity);
    }

    public async Task UpdateActivityStatsAsync(string activityId, ActivityStats stats) => await _diagramDesigner.UpdateActivityStatsAsync(activityId, stats);

    public JsonObject? GetCurrentActivity()
    {
        var resolvedPath = ResolvePath().LastOrDefault();
        return resolvedPath?.Activity;
    }

    public JsonObject GetCurrentContainerActivity()
    {
        var resolvedPath = ResolvePath().LastOrDefault();
        return resolvedPath?.EmbeddedActivity ?? Activity;
    }

    protected override async Task OnInitializedAsync()
    {
        await ActivityRegistry.EnsureLoadedAsync();
        _diagramDesigner = DiagramDesignerService.GetDiagramDesigner(Activity);
        await UpdatePathSegmentsAsync(segments => segments.Clear());
    }

    private IEnumerable<GraphSegment> ResolvePath()
    {
        var currentContainer = Activity;

        foreach (var pathSegment in _pathSegments.Reverse())
        {
            var flowchart = currentContainer.GetFlowchart();
            var activities = flowchart.GetActivities();
            var currentActivity = activities.First(x => x.GetId() == pathSegment.ActivityId);
            var portName = pathSegment.PortName;
            var activityTypeName = currentActivity.GetTypeName();
            var activityVersion = currentActivity.GetVersion();
            var activityDescriptor = ActivityRegistry.Find(activityTypeName, activityVersion)!;
            var portProviderContext = new PortProviderContext(activityDescriptor, currentActivity);
            var portProvider = ActivityPortService.GetProvider(portProviderContext);

            currentContainer = portProvider.ResolvePort(portName, portProviderContext)!;

            var segment = new GraphSegment(currentActivity, pathSegment.PortName, currentContainer);
            yield return segment;
        }
    }

    private async Task UpdatePathSegmentsAsync(Action<Stack<ActivityPathSegment>> action)
    {
        action(_pathSegments);
        await UpdateBreadcrumbItemsAsync();
    }

    private async Task UpdateBreadcrumbItemsAsync()
    {
        var currentContainerActivity = GetCurrentContainerActivity();
        _breadcrumbItems = (await GetBreadcrumbItems()).ToList();

        if (WorkflowInstanceId != null)
        {
            var report = await ActivityExecutionService.GetReportAsync(WorkflowInstanceId, currentContainerActivity);
            _activityStats = report.Stats.ToDictionary(x => x.ActivityId, x => new ActivityStats
            {
                Faulted = x.IsFaulted,
                Blocked = x.IsBlocked,
                Completed = x.CompletedCount,
                Started = x.StartedCount,
                Uncompleted = x.UncompletedCount
            });
        }

        if (PathChanged != null)
            await PathChanged(new DesignerPathChangedArgs(currentContainerActivity));

        StateHasChanged();
    }

    private Task<IEnumerable<BreadcrumbItem>> GetBreadcrumbItems()
    {
        var breadcrumbItems = new List<BreadcrumbItem>();

        if (_pathSegments.Any())
            breadcrumbItems.Add(new BreadcrumbItem("Root", "#_root_", false, Icons.Material.Outlined.Home));

        var resolvedPath = ResolvePath().ToList();

        foreach (var segment in resolvedPath)
        {
            var currentActivity = segment.Activity;
            var activityTypeName = currentActivity.GetTypeName();
            var activityVersion = currentActivity.GetVersion();
            var activityDescriptor = ActivityRegistry.Find(activityTypeName, activityVersion)!;
            var portProviderContext = new PortProviderContext(activityDescriptor, currentActivity);
            var portProvider = ActivityPortService.GetProvider(portProviderContext);
            var ports = portProvider.GetPorts(portProviderContext);
            var embeddedPort = ports.First(x => x.Name == segment.PortName);
            var displaySettings = ActivityDisplaySettingsRegistry.GetSettings(activityTypeName);
            var disabled = segment == resolvedPath.Last();
            var activityDisplayText = currentActivity.GetName() ?? activityDescriptor.DisplayName;
            var breadcrumbDisplayText = $"{activityDisplayText}: {embeddedPort.DisplayName}";
            var activityBreadcrumbItem = new BreadcrumbItem(breadcrumbDisplayText, $"#{currentActivity.GetId()}", disabled, displaySettings.Icon);

            breadcrumbItems.Add(activityBreadcrumbItem);
        }

        return Task.FromResult<IEnumerable<BreadcrumbItem>>(breadcrumbItems);
    }

    private async Task DisplayCurrentSegmentAsync()
    {
        var currentContainerActivity = GetCurrentContainerActivity();

        if (_diagramDesigner == null)
            return;

        await _diagramDesigner.LoadRootActivityAsync(currentContainerActivity, _activityStats);
    }

    private async Task OnActivityEmbeddedPortSelected(ActivityEmbeddedPortSelectedArgs args)
    {
        var activity = args.Activity;
        var portName = args.PortName;
        var activityTypeName = activity.GetTypeName();
        var activityVersion = activity.GetVersion();
        var activityDescriptor = ActivityRegistry.Find(activityTypeName, activityVersion)!;
        var portProviderContext = new PortProviderContext(activityDescriptor, activity);
        var portProvider = ActivityPortService.GetProvider(portProviderContext);
        var embeddedActivity = portProvider.ResolvePort(portName, portProviderContext);

        if (embeddedActivity != null)
        {
            var embeddedActivityTypeName = embeddedActivity.GetTypeName();

            // If the embedded activity has no designer support, then open it in the activity properties editor by raising the ActivitySelected event.
            if (embeddedActivityTypeName != "Elsa.Flowchart" && embeddedActivityTypeName != "Elsa.Workflow")
            {
                ActivitySelected?.Invoke(embeddedActivity);
                return;
            }

            // If the embedded activity type is a flowchart or workflow, we can display it in the designer.
        }
        else
        {
            if (!IsReadOnly)
            {
                // Create a flowchart and embed it into the activity.
                embeddedActivity = new JsonObject(new Dictionary<string, JsonNode?>
                {
                    ["id"] = ActivityIdGenerator.GenerateId(),
                    ["type"] = "Elsa.Flowchart",
                    ["version"] = 1,
                    ["name"] = "Flowchart1",
                });

                portProvider.AssignPort(args.PortName, embeddedActivity, new PortProviderContext(activityDescriptor, activity));

                // Update the graph in the designer.
                await _diagramDesigner!.UpdateActivityAsync(activity.GetId(), activity);
            }
            else
            {
                return;
            }
        }

        // Create a new path segment of the container activity and push it onto the stack.
        var segment = new ActivityPathSegment(activity.GetId(), activity.GetTypeName(), args.PortName);

        await UpdatePathSegmentsAsync(segments => segments.Push(segment));
        await DisplayCurrentSegmentAsync();
    }

    private async Task OnGraphUpdated()
    {
        var rootActivity = await _diagramDesigner!.ReadRootActivityAsync();
        var currentActivity = GetCurrentActivity();
        var currentSegment = CurrentPathSegment;

        if (currentActivity == null || currentSegment == null)
            Activity = rootActivity;
        else
        {
            var portName = currentSegment.PortName;
            var activityTypeName = currentActivity.GetTypeName();
            var activityVersion = currentActivity.GetVersion();
            var activityDescriptor = ActivityRegistry.Find(activityTypeName, activityVersion)!;
            var portProviderContext = new PortProviderContext(activityDescriptor, currentActivity);
            var portProvider = ActivityPortService.GetProvider(portProviderContext);

            portProvider.AssignPort(portName, rootActivity, portProviderContext);
        }

        if (GraphUpdated != null)
            await GraphUpdated();
    }

    private async Task OnBreadcrumbItemClicked(BreadcrumbItem item)
    {
        if (item.Href == "#_root_")
        {
            await UpdatePathSegmentsAsync(segments => segments.Clear());
            await DisplayCurrentSegmentAsync();
            return;
        }

        var activityId = item.Href![1..];

        await UpdatePathSegmentsAsync(segments =>
        {
            while (segments.TryPeek(out var segment))
                if (segment.ActivityId == activityId)
                    break;
                else
                    segments.Pop();
        });

        await DisplayCurrentSegmentAsync();
    }
}