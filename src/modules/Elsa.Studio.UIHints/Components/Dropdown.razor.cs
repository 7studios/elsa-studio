using Elsa.Api.Client.Expressions;
using Elsa.Studio.Models;
using Elsa.Studio.UIHints.Extensions;
using Elsa.Studio.UIHints.Models;
using Microsoft.AspNetCore.Components;

namespace Elsa.Studio.UIHints.Components;

public partial class Dropdown
{
    private ICollection<SelectListItem> _items = Array.Empty<SelectListItem>();

    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = default!;
    
    protected override void OnInitialized()
    {
        var selectList = EditorContext.InputDescriptor.GetSelectList();
        _items = selectList.Items.OrderBy(x => x.Text).ToList();
    }

    private SelectListItem? GetSelectedValue()
    {
        var value = EditorContext.GetLiteralValueOrDefault();
        return _items.FirstOrDefault(x => x.Value == value);
    }
    
    private async Task OnValueChanged(SelectListItem? value)
    {
        var expression = new LiteralExpression(value?.Value);
        await EditorContext.UpdateExpressionAsync(expression);
    }
}