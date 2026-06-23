using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ComboBox.Components;

public partial class Select<TItem> : ComponentBase, IAsyncDisposable
{
    private readonly string _triggerId = $"select-trigger-{Guid.NewGuid()}";
    private readonly string _listboxId = $"select-listbox-{Guid.NewGuid()}";

    [Inject]
    private IJSRuntime? Js { get; set; }

    [Parameter]
    public TItem? Value { get; set; }

    [Parameter]
    public EventCallback<TItem?> ValueChanged { get; set; }

    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    [Parameter]
    public Func<CancellationToken, ValueTask<IEnumerable<TItem>>>? DataProvider { get; set; }

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public Func<TItem, string>? ToStringFunc { get; set; }

    [Parameter]
    public IEqualityComparer<TItem>? EqualityComparer { get; set; }

    [Parameter]
    public bool ShowCheckmarkInList { get; set; } = true;

    [Parameter]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    [Parameter]
    public RenderFragment<TItem>? SelectedTemplate { get; set; }

    [Parameter]
    public RenderFragment? NoResultsTemplate { get; set; }

    [Parameter]
    public string NoResultsText { get; set; } = "No options.";

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? InputClass { get; set; }

    [Parameter]
    public string? DropdownClass { get; set; }

    [Parameter]
    public string? DropdownWidth { get; set; }

    [Parameter]
    public DropdownAnchorPosition AnchorPosition { get; set; } = DropdownAnchorPosition.Left;

    [Parameter]
    public string? Label { get; set; }

    private string PortalId { get; } = $"portal-{Guid.NewGuid()}";

    private string Classes => new ClassBuilder()
        .AddClass("combo-container select-container")
        .AddClass("combo-disabled", Disabled)
        .AddClass(Class)
        .Build();

    private string InputClasses => new ClassBuilder()
        .AddClass("combo-trigger select-trigger")
        .AddClass(InputClass)
        .Build();

    private string DropdownClasses => new ClassBuilder()
        .AddClass("combo-dropdown select-dropdown")
        .AddClass(DropdownClass)
        .Build();

    private string TriggerAriaLabel => string.IsNullOrWhiteSpace(Label) ? Placeholder : Label;
    private List<TItem> ItemsList { get; set; } = [];

    private ElementReference? _rootRef;
    private ElementReference _triggerRef;
    private IJSObjectReference? _outsideClickListener;
    private DotNetObjectReference<Select<TItem>>? _dotNetObjectReference;
    private CancellationTokenSource? _loadCts;
    private bool IsOpen { get; set; }
    private bool IsLoading { get; set; }
    private int _highlightedIndex;

    protected override void OnParametersSet()
    {
        var hasItems = Items is not null;
        var hasDataProvider = DataProvider is not null;

        if (hasItems && hasDataProvider)
            throw new InvalidOperationException("Set either Items or DataProvider, not both.");

        if (!hasItems && !hasDataProvider)
            throw new InvalidOperationException("Select requires either Items or DataProvider.");

        if (Items is not null)
        {
            ItemsList = Items.ToList();
            SetHighlightedIndexToSelectedItem();
        }
    }

    private bool IsSelectedItem(TItem item)
    {
        return Value is not null && ItemEquals(Value, item);
    }

    private bool ItemEquals(TItem? left, TItem? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return (EqualityComparer ?? System.Collections.Generic.EqualityComparer<TItem>.Default).Equals(left, right);
    }

    private async Task ToggleDropdown()
    {
        if (Disabled)
            return;

        if (IsOpen)
            await CloseDropdownAsync(true);
        else
            await OpenDropdownAsync();
    }

    private async Task OpenDropdownAsync()
    {
        IsOpen = true;

        SetHighlightedIndexToSelectedItem();

        _dotNetObjectReference = DotNetObjectReference.Create(this);
        if (Js is not null)
        {
            _outsideClickListener = await Js.InvokeAsync<IJSObjectReference>(
                "comboBoxRegisterOutsideClick",
                _rootRef,
                _dotNetObjectReference,
                PortalId
            );
        }

        if (DataProvider is not null)
            await LoadItemsAsync();
    }

    private async Task CloseDropdownAsync(bool returnFocusToTrigger)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        CancelLoad();
        await RemoveOutsideClickListener();

        if (returnFocusToTrigger)
            await _triggerRef.FocusAsync();
    }

    private string GetItemString(TItem item)
    {
        if (ToStringFunc is not null)
            return ToStringFunc(item);

        return item?.ToString() ?? "";
    }

    private async Task LoadItemsAsync()
    {
        CancelLoad();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        StateHasChanged();

        try
        {
            var items = await DataProvider!(token);
            if (token.IsCancellationRequested)
                return;

            ItemsList = items.ToList();
            SetHighlightedIndexToSelectedItem();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void SetHighlightedIndexToSelectedItem()
    {
        var selectedIndex = ItemsList.FindIndex(IsSelectedItem);
        _highlightedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private bool IsHighlighted(TItem item)
    {
        if (_highlightedIndex < 0 || _highlightedIndex >= ItemsList.Count)
            return false;

        return ItemEquals(ItemsList[_highlightedIndex], item);
    }

    private string GetOptionId(TItem item)
    {
        var itemString = GetItemString(item);
        var hash = unchecked((uint)itemString.GetHashCode(StringComparison.Ordinal));
        return $"{_listboxId}-option-{hash}";
    }

    private async Task HandleTriggerKeyDown(KeyboardEventArgs e)
    {
        if (Disabled)
            return;

        if (e.Key is "ArrowDown" or "ArrowUp")
        {
            if (!IsOpen)
                await OpenDropdownAsync();

            MoveHighlight(e.Key == "ArrowDown" ? 1 : -1);
            StateHasChanged();
            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "Escape")
        {
            await CloseDropdownAsync(true);
            StateHasChanged();
        }
    }

    private void MoveHighlight(int offset)
    {
        if (ItemsList.Count == 0)
            return;

        _highlightedIndex = Math.Clamp(_highlightedIndex + offset, 0, ItemsList.Count - 1);
    }

    private async Task ScrollHighlightedItemIntoView()
    {
        if (Js is not null && _rootRef.HasValue)
            await Js.InvokeVoidAsync("comboBoxScrollToHighlighted", _rootRef, PortalId);
    }

    private async Task ItemClicked(TItem item)
    {
        Value = item;
        await CloseDropdownAsync(true);
        await ValueChanged.InvokeAsync(item);
    }

    [JSInvokable]
    public async Task CloseDropdown()
    {
        await CloseDropdownAsync(false);
        StateHasChanged();
    }

    private async Task RemoveOutsideClickListener()
    {
        if (_outsideClickListener is not null)
        {
            await _outsideClickListener.InvokeVoidAsync("dispose");
            await _outsideClickListener.DisposeAsync();
            _outsideClickListener = null;
        }

        _dotNetObjectReference?.Dispose();
        _dotNetObjectReference = null;
    }

    private void CancelLoad()
    {
        if (_loadCts is null)
            return;

        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = null;
        IsLoading = false;
    }

    public async ValueTask DisposeAsync()
    {
        CancelLoad();
        await RemoveOutsideClickListener();

        _dotNetObjectReference = null;
        _outsideClickListener = null;

        if (Js is not null)
            await Js.InvokeVoidAsync("portalHelper.removeFromBody", PortalId);
    }
}
