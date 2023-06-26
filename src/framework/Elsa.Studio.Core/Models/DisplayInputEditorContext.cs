using System.Text.Json;
using Elsa.Api.Client.Activities;
using Elsa.Api.Client.Contracts;
using Elsa.Api.Client.Expressions;
using Elsa.Api.Client.Models;
using Elsa.Api.Client.Resources.ActivityDescriptors.Models;
using Elsa.Studio.Contracts;

namespace Elsa.Studio.Models;

public class DisplayInputEditorContext
{
    public Activity Activity { get; set; } = default!;
    public ActivityDescriptor ActivityDescriptor { get; set; } = default!;
    public InputDescriptor InputDescriptor { get; set; } = default!;
    public ActivityInput? Value { get; set; }
    public IUIHintHandler UIHintHandler { get; set; } = default!;
    public ISyntaxProvider? SelectedSyntaxProvider { get; set; }
    public Func<ActivityInput, Task> OnValueChanged { get; set; } = default!;

    public async Task UpdateExpressionAsync(IExpression expression)
    {
        // Update input.
        var input = Value ?? new ActivityInput();
        input.Expression = expression;
        
        Value = input;
        
        // Notify that the input has changed.
        await OnValueChanged(input);
    }
}