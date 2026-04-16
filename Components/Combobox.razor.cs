using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace ComboBox.Components;

public partial class Combobox<TItem> : ComponentBase, IAsyncDisposable
{
    private string _triggerId = $"combo-trigger-{Guid.NewGuid()}";
    private string _listboxId = $"combo-listbox-{Guid.NewGuid()}";
    [Inject] private IJSRuntime? Js { get; set; }

    /// <summary>
    /// The current selected value of the component.
    /// </summary>
    [Parameter]
    public TItem? Value { get; set; }

    /// <summary>
    /// Callback invoked when the selected value changes.
    /// </summary>
    [Parameter]
    public EventCallback<TItem?> ValueChanged { get; set; }

    [Parameter]
    public EventCallback<string?> AddOptionChanged { get; set; }

    /// <summary>
    /// Placeholder text displayed when no value is selected.
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "";

    /// <summary>
    /// Function to convert an item of type <typeparamref name="TItem"/> to its string representation.
    /// </summary>
    [Parameter]
    public Func<TItem, string>? ToStringFunc { get; set; }

    [Parameter]
    public IEqualityComparer<TItem>? EqualityComparer { get; set; }

    /// <summary>
    /// Determines whether a checkmark is shown next to selected items in the list.
    /// </summary>
    [Parameter]
    public bool ShowCheckmarkInList { get; set; } = true;

    /// <summary>
    /// Optional provider for asynchronous data loading, taking a single <see cref="ComboState"/> object and a <see cref="CancellationToken"/>.
    /// </summary>
    [Parameter]
    public Func<ComboState, CancellationToken, ValueTask<ItemsProviderResult<TItem>>>? DataProvider { get; set; }

    /// <summary>
    /// A static list of items to display when using preloaded data instead of a data provider.
    /// </summary>
    [Parameter]
    public List<TItem>? StaticData { get; set; }

    /// <summary>
    /// Template for rendering each item in the dropdown list.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>
    /// Template for rendering the selected item when the dropdown is closed.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? SelectedTemplate { get; set; }

    /// <summary>
    /// Template displayed when no matching items are found.
    /// </summary>
    [Parameter]
    public RenderFragment? NoResultsTemplate { get; set; }
    
    [Parameter]
    public bool ShowAddOption { get; set; } = false;

    [Parameter]
    public string AddOptionText { get; set; } = "Add.";

    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    [Parameter]
    public string NoResultsText { get; set; } = "No results.";
    [Parameter]
    public string? AdornmentIcon { get; set; }

    /// <summary>
    /// If set to <c>true</c>, the component is disabled and user interaction is prevented.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// CSS class applied to the root element of the component.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// CSS class applied to the input element.
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// CSS class applied to the dropdown list container.
    /// </summary>
    [Parameter]
    public string? DropdownClass { get; set; }

    /// <summary>
    /// Optional additional CSS width value added to the trigger width for the dropdown panel
    /// (for example: 120px, 8rem, 10vw).
    /// If not set, the dropdown uses the same width as the trigger.
    /// </summary>
    [Parameter]
    public string? DropdownWidth { get; set; }

    /// <summary>
    /// Controls where the dropdown width expansion is anchored relative to the trigger.
    /// </summary>
    [Parameter]
    public DropdownAnchorPosition AnchorPosition { get; set; } = DropdownAnchorPosition.Left;

    /// <summary>
    /// Label text displayed for the component, if applicable.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// The fixed pixel height of each item in the virtualized list.
    /// </summary>
    [Parameter]
    public float ItemSize { get; set; } = 50f;

    [Parameter]
    public int SearchDebounceMs { get; set; } = 300;
    
    private string PortalId { get; } = $"portal-{Guid.NewGuid()}";


    private string Classes => new ClassBuilder()
        .AddClass("combo-container")
        .AddClass("combo-disabled", Disabled)
        .AddClass(Class)
        .Build();

    private string InputClasses => new ClassBuilder()
        .AddClass("combo-trigger")
        .AddClass(InputClass)
        .Build();

    private string DropdownClasses => new ClassBuilder()
        .AddClass("combo-dropdown")
        .AddClass(DropdownClass)
        .Build();

    private string TriggerAriaLabel => string.IsNullOrWhiteSpace(Label) ? Placeholder : Label;

    private ElementReference? _rootRef;
    private ElementReference _triggerRef;
    private bool IsLoading { get; set; }
    private IJSObjectReference? _outsideClickListener;
    private DotNetObjectReference<Combobox<TItem>>? _dotNetObjectReference;
    private CancellationTokenSource? _debounceCts;
    private string? SearchText { get; set; }
    private Virtualize<TItem>? _virtualizeRef;
    private bool IsOpen { get; set; }
    private ElementReference _elementReference;
    private List<TItem> _visibleItems = [];
    private int _highlightedIndex;
    private int _totalItemCount = -1;
    private bool _skipNextToggle;
    private bool _bodyScrollLocked;
    private bool ShowNoResults => !IsLoading && _totalItemCount == 0;
    private string? ActiveDescendantId =>
        IsOpen && _highlightedIndex >= 0 && _highlightedIndex < _visibleItems.Count
            ? GetOptionId(_visibleItems[_highlightedIndex])
            : null;

    protected override void OnParametersSet()
    {
        var hasDataProvider = DataProvider is not null;
        var hasStaticData = StaticData is not null;

        if (hasDataProvider && hasStaticData)
            throw new InvalidOperationException("Set either DataProvider or StaticData, not both.");

        if (!hasDataProvider && !hasStaticData)
            throw new InvalidOperationException("Combobox requires either DataProvider or StaticData.");

        if (SearchDebounceMs < 0)
            throw new InvalidOperationException("SearchDebounceMs cannot be negative.");

        if (ShowAddOption && !AddOptionChanged.HasDelegate)
            throw new InvalidOperationException("ShowAddOption requires AddOptionChanged callback.");

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

        if (_skipNextToggle)
        {
            _skipNextToggle = false;
            return;
        }

        if (IsOpen)
            await CloseDropdownAsync(true);
        else
            await OpenDropdownAsync();
    }

    private async Task OpenDropdownAsync()
    {
        IsOpen = true;
        SearchText = "";
        _totalItemCount = -1;
        _highlightedIndex = 0;
        _ = Task.Delay(10).ContinueWith(_ => { InvokeAsync(() => _elementReference.FocusAsync()); });
        _ = RefreshItems();

        _dotNetObjectReference = DotNetObjectReference.Create(this);
        if (Js != null)
        {
            _outsideClickListener = await Js.InvokeAsync<IJSObjectReference>(
                "comboBoxRegisterOutsideClick",
                _rootRef,
                _dotNetObjectReference,
                PortalId
            );

            await Js.InvokeVoidAsync("comboBodyScroll.lock");
            _bodyScrollLocked = true;
        }
    }

    private async Task CloseDropdownAsync(bool returnFocusToTrigger)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        await RemoveOutsideClickListener();

        if (_bodyScrollLocked && Js != null)
        {
            await Js.InvokeVoidAsync("comboBodyScroll.unlock");
            _bodyScrollLocked = false;
        }

        if (returnFocusToTrigger)
            await _triggerRef.FocusAsync();
    }

    private string GetItemString(TItem item)
    {
        if (ToStringFunc != null)
            return ToStringFunc(item);
        return item?.ToString() ?? "";
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        SearchText = e.Value?.ToString() ?? "";
        _totalItemCount = -1;
        _highlightedIndex = 0;

        if (_debounceCts != null)
        {
            _debounceCts.Cancel();
            _debounceCts.Dispose();
            _debounceCts = null;
        }

        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(Math.Max(0, SearchDebounceMs), token);
            await RefreshItems();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task RefreshItems()
    {
        if (_virtualizeRef != null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }


    private async ValueTask<ItemsProviderResult<TItem>> LoadItems(
        ItemsProviderRequest request)
    {
        if (DataProvider is not null)
        {
            //Server-side:
            IsLoading = true;
            StateHasChanged();

            try
            {
                // Invoke the provider and honor cancellation
                var result = await DataProvider(
                    new ComboState(SearchText, request.Count, request.StartIndex),
                    request.CancellationToken
                );

                _visibleItems = result.Items.ToList();
                _totalItemCount = result.TotalItemCount;
                return result;
            }
            catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
            {
                _totalItemCount = -1;
                return new ItemsProviderResult<TItem>(Array.Empty<TItem>(), 0);
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        if (StaticData is not null)
        {
            // Client‐side:
            var filtered = string.IsNullOrEmpty(SearchText)
                ? StaticData
                : StaticData.Where(item => ItemMatches(item, SearchText)).ToList();

            var page = filtered
                .Skip(request.StartIndex)
                .Take(request.Count)
                .ToList();

            _visibleItems = page;
            _totalItemCount = filtered.Count;
            return new ItemsProviderResult<TItem>(page, filtered.Count);
        }

        // No data
        _totalItemCount = 0;
        return new ItemsProviderResult<TItem>(Array.Empty<TItem>(), 0);
    }


    private bool ItemMatches(TItem item, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var str = GetItemString(item);
        return str.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHighlighted(TItem item)
    {
        if (_highlightedIndex < 0 || _highlightedIndex >= _visibleItems.Count)
            return false;

        var highlighted = _visibleItems[_highlightedIndex];
        return ItemEquals(highlighted, item);
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
            {
                await OpenDropdownAsync();

                if (e.Key == "ArrowUp" && _visibleItems.Count > 0)
                    _highlightedIndex = _visibleItems.Count - 1;
            }

            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "Escape")
        {
            await CloseDropdownAsync(true);
            StateHasChanged();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Disabled)
            return;

        if (e.Key == "Escape")
        {
            await CloseDropdownAsync(true);
            StateHasChanged();
            return;
        }

        if (_visibleItems.Count == 0)
            return;

        if (e.Key == "ArrowDown")
        {
            _highlightedIndex = Math.Min(_highlightedIndex + 1, _visibleItems.Count - 1);
            StateHasChanged();
            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "ArrowUp")
        {
            _highlightedIndex = Math.Max(_highlightedIndex - 1, 0);
            StateHasChanged();
            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "Enter")
        {
            if (_highlightedIndex >= 0 && _highlightedIndex < _visibleItems.Count)
            {
                _skipNextToggle = true;
                await ItemClicked(_visibleItems[_highlightedIndex]);
            }
        }
    }

    private async Task ScrollHighlightedItemIntoView()
    {
        if (Js != null && _rootRef.HasValue)
        {
            await Js.InvokeVoidAsync("comboBoxScrollToHighlighted", _rootRef, PortalId);
        }
    }

    private async Task ItemClicked(TItem item)
    {
        Value = item;
        await CloseDropdownAsync(true);
        await ValueChanged.InvokeAsync(item);
    }

    private async Task AddClicked(string newItem)
    {
        var normalized = string.IsNullOrWhiteSpace(newItem) ? null : newItem.Trim();
        if (string.IsNullOrEmpty(normalized))
            return;

        await AddOptionChanged.InvokeAsync(normalized);
    }

    [JSInvokable] public async Task CloseDropdown()
    {
        await CloseDropdownAsync(false);
        StateHasChanged();
    }

    private async Task RemoveOutsideClickListener()
    {
        if (_outsideClickListener != null)
        {
            await _outsideClickListener.InvokeVoidAsync("dispose");
            await _outsideClickListener.DisposeAsync();
            _outsideClickListener = null;
        }

        _dotNetObjectReference?.Dispose();
        _dotNetObjectReference = null;
    }


    public async ValueTask DisposeAsync()
    {
        if (_debounceCts is not null)
        {
            _debounceCts.Cancel();
            _debounceCts.Dispose();
        }

        await RemoveOutsideClickListener();

        _dotNetObjectReference = null;
        _debounceCts = null;
        _outsideClickListener = null;
        _virtualizeRef = null;

        if (_bodyScrollLocked && Js != null)
        {
            await Js.InvokeVoidAsync("comboBodyScroll.unlock");
            _bodyScrollLocked = false;
        }

        if (Js != null)
        {
            await Js.InvokeVoidAsync("portalHelper.removeFromBody", PortalId);
        }
    }
}
