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

    /// <summary>
    /// When true several items can be selected: a pick toggles the item in <see cref="Values"/>
    /// and the dropdown stays open. <see cref="Value"/> is not used.
    /// </summary>
    [Parameter]
    public bool MultiSelect { get; set; }

    /// <summary>
    /// The selected items when <see cref="MultiSelect"/> is true, in the order they were picked.
    /// </summary>
    [Parameter]
    public IReadOnlyList<TItem>? Values { get; set; }

    /// <summary>
    /// Invoked with a new list each time an item is toggled when <see cref="MultiSelect"/> is true.
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<TItem>> ValuesChanged { get; set; }

    /// <summary>
    /// The trigger text when <see cref="MultiSelect"/> is true and something is selected.
    /// Defaults to the selected items joined with commas, using <see cref="ToStringFunc"/>.
    /// </summary>
    [Parameter]
    public Func<IReadOnlyList<TItem>, string>? MultiSelectionTextFunc { get; set; }

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
    public Func<TItem, bool>? ItemDisabledFunc { get; set; }

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

    /// <summary>
    /// Template for the content of each row, with the row's state (selected, highlighted, disabled).
    /// <c>SearchText</c> is always empty. Takes precedence over <see cref="ItemTemplate"/> when both are set.
    /// </summary>
    [Parameter]
    public RenderFragment<ComboItemContext<TItem>>? ItemContentTemplate { get; set; }

    /// <summary>
    /// Shows a clear (×) button next to the chevron while there is a value (<see cref="Value"/> or
    /// <see cref="Values"/>). Clicking it resets the value, invokes <see cref="OnClear"/> and focuses the trigger.
    /// </summary>
    [Parameter]
    public bool Clearable { get; set; }

    /// <summary>Accessible name of the clear button shown with <see cref="Clearable"/>.</summary>
    [Parameter]
    public string ClearButtonAriaLabel { get; set; } = "Clear selection";

    /// <summary>Invoked after the clear button has reset the value.</summary>
    [Parameter]
    public EventCallback OnClear { get; set; }

    /// <summary>Shows a spinner in the trigger next to the chevron and sets <c>aria-busy</c>. The trigger stays usable.</summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Shown in the dropdown when the <see cref="DataProvider"/> throws. The context carries the exception
    /// and a <c>Retry</c> function that calls <see cref="RefreshAsync"/>.
    /// </summary>
    [Parameter]
    public RenderFragment<ComboErrorContext>? ErrorTemplate { get; set; }

    /// <summary>Text of the default error row (when <see cref="ErrorTemplate"/> is not set), also announced to screen readers.</summary>
    [Parameter]
    public string ErrorText { get; set; } = "Something went wrong.";

    /// <summary>Text shown while the <see cref="DataProvider"/> loads.</summary>
    [Parameter]
    public string LoadingText { get; set; } = "Loading...";

    /// <summary>Replaces the loading text and progress dots while the <see cref="DataProvider"/> loads.</summary>
    [Parameter]
    public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>Announced to screen readers (aria-live) while the <see cref="DataProvider"/> loads.</summary>
    [Parameter]
    public string LoadingAnnouncementText { get; set; } = "Loading results";

    /// <summary>Static content rendered at the bottom of the dropdown, e.g. a "Done" button calling <see cref="CloseAsync"/>.</summary>
    [Parameter]
    public RenderFragment? FooterTemplate { get; set; }

    /// <summary>Id(s) of elements describing the field (help or error text), set as <c>aria-describedby</c> on the trigger.</summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>Sets <c>aria-invalid="true"</c> on the trigger and the class <c>combo-invalid</c> on the root.</summary>
    [Parameter]
    public bool Invalid { get; set; }

    /// <summary>
    /// Id(s) of an external label, set as <c>aria-labelledby</c> on the trigger and the listbox instead of
    /// the <c>aria-label</c> built from <see cref="Label"/> or <see cref="Placeholder"/>.
    /// </summary>
    [Parameter]
    public string? AriaLabelledBy { get; set; }

    /// <summary>
    /// Leading content in the trigger before the value or placeholder, e.g. an icon or an avatar.
    /// Shown also when nothing is selected. Must not contain interactive elements (it sits inside a button).
    /// </summary>
    [Parameter]
    public RenderFragment? StartContent { get; set; }

    /// <summary>Minimum dropdown width as a CSS length. The dropdown is never narrower than the trigger and stays inside the viewport.</summary>
    [Parameter]
    public string? DropdownMinWidth { get; set; }

    /// <summary>Maximum dropdown width as a CSS length. It does not shrink the dropdown below the trigger width.</summary>
    [Parameter]
    public string? DropdownMaxWidth { get; set; }

    private string PortalId { get; } = $"portal-{Guid.NewGuid()}";

    private string Classes => new ClassBuilder()
        .AddClass("combo-container select-container")
        .AddClass("combo-disabled", Disabled)
        .AddClass("combo-invalid", Invalid)
        .AddClass(Class)
        .Build();

    private string InputClasses => new ClassBuilder()
        .AddClass("combo-trigger select-trigger")
        .AddClass("combo-trigger-clearable", ShowClearValueButton)
        .AddClass(InputClass)
        .Build();

    private string DropdownClasses => new ClassBuilder()
        .AddClass("combo-dropdown select-dropdown")
        .AddClass(DropdownClass)
        .Build();

    private string? TriggerAriaLabel => !string.IsNullOrWhiteSpace(AriaLabelledBy) ? null
        : string.IsNullOrWhiteSpace(Label) ? Placeholder : Label;

    private string? AriaLabelledByOrNull => string.IsNullOrWhiteSpace(AriaLabelledBy) ? null : AriaLabelledBy;

    private string? AriaDescribedByOrNull => string.IsNullOrWhiteSpace(AriaDescribedBy) ? null : AriaDescribedBy;

    private bool HasValue => MultiSelect ? Values is { Count: > 0 } : Value is not null;

    private bool ShowClearValueButton => Clearable && !Disabled && HasValue;

    private string? TriggerAriaInvalid => Invalid ? "true" : null;

    private string? TriggerAriaBusy => Loading ? "true" : null;

    // Set when the DataProvider threw; cleared when the next load starts.
    private Exception? _loadError;
    private List<TItem> ItemsList { get; set; } = [];

    private ElementReference? _rootRef;
    private ElementReference _triggerRef;
    private IJSObjectReference? _outsideClickListener;
    private DotNetObjectReference<Select<TItem>>? _dotNetObjectReference;
    private CancellationTokenSource? _loadCts;
    private bool IsOpen { get; set; }
    private bool IsLoading { get; set; }
    private bool _skipNextToggle;
    private bool _hasRendered;
    private int _highlightedIndex;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            _hasRendered = true;
    }

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

            // A bound parent re-renders after every toggle; the highlight must stay where the user left it.
            if (MultiSelect && IsOpen)
                EnsureHighlightedIndexInRange();
            else
                SetHighlightedIndexToSelectedItem();
        }
    }

    private string MultiSelectionText =>
        Values is null ? "" : MultiSelectionTextFunc?.Invoke(Values) ?? MultiSelection.DefaultText(Values, GetItemString);

    private bool IsSelectedItem(TItem item)
    {
        if (MultiSelect)
            return MultiSelection.Contains(Values, item, ItemEquals);

        return Value is not null && ItemEquals(Value, item);
    }

    // A bool would render as aria-selected="", which assistive technology reads as not selected.
    // A multi-select listbox marks every option; a single-select one only the selected option.
    private string? AriaSelected(TItem item) => IsSelectedItem(item) ? "true" : MultiSelect ? "false" : null;

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

        SetHighlightedIndexToSelectedItem();

        _dotNetObjectReference = DotNetObjectReference.Create(this);
        if (Js is not null)
        {
            // In multi-select mode the dropdown stays open after a click, so focus must stay on
            // the trigger for the keyboard to keep working.
            _outsideClickListener = await Js.InvokeAsync<IJSObjectReference>(
                "comboBoxRegisterOutsideClick",
                _rootRef,
                _dotNetObjectReference,
                PortalId,
                MultiSelect,
                MultiSelect
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
        _loadError = null;
        StateHasChanged();

        try
        {
            var items = await DataProvider!(token);
            if (token.IsCancellationRequested)
                return;

            ItemsList = items.ToList();
            SetHighlightedIndexToSelectedItem();
        }
        catch (Exception exception)
        {
            // A cancelled load was replaced or the dropdown closed. Anything else is shown through
            // ErrorTemplate / ErrorText instead of surfacing as an unhandled exception in the event handler.
            if (!token.IsCancellationRequested)
            {
                _loadError = exception;
                ItemsList = [];
                _highlightedIndex = -1;
            }
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
        var selectedIndex = ItemsList.FindIndex(item => IsSelectedItem(item) && !IsItemDisabled(item));
        _highlightedIndex = selectedIndex >= 0 ? selectedIndex : FindNextEnabledIndex(0, 1);
    }

    private void EnsureHighlightedIndexInRange()
    {
        // A disabled item may be highlighted: the user can arrow onto it.
        if (_highlightedIndex < 0 || _highlightedIndex >= ItemsList.Count)
            _highlightedIndex = FindNextEnabledIndex(0, 1);
    }

    private bool IsHighlighted(TItem item)
    {
        if (_highlightedIndex < 0 || _highlightedIndex >= ItemsList.Count)
            return false;

        return ItemEquals(ItemsList[_highlightedIndex], item);
    }

    private bool IsItemDisabled(TItem item)
    {
        return ItemDisabledFunc?.Invoke(item) == true;
    }

    private string? AriaDisabled(TItem item) => IsItemDisabled(item) ? "true" : null;

    private ComboItemContext<TItem> ItemContext(TItem item) =>
        new(item, IsSelectedItem(item), IsHighlighted(item), IsItemDisabled(item), "");

    private ComboErrorContext ErrorContext => new(_loadError!, RefreshAsync);

    private bool HasHighlightedItem => _highlightedIndex >= 0 && _highlightedIndex < ItemsList.Count;

    /// <summary>
    /// Reloads the items from <see cref="DataProvider"/> while the dropdown is open.
    /// Does nothing while it is closed or when <see cref="Items"/> is used.
    /// </summary>
    public Task RefreshAsync() => InvokeAsync(async () =>
    {
        if (!IsOpen || DataProvider is null)
            return;

        await LoadItemsAsync();
    });

    /// <summary>
    /// Closes the dropdown and returns focus to the trigger, e.g. from a "Done" button in <see cref="FooterTemplate"/>.
    /// Does nothing while the dropdown is closed.
    /// </summary>
    public Task CloseAsync() => InvokeAsync(async () =>
    {
        if (!IsOpen)
            return;

        await CloseDropdownAsync(true);
        StateHasChanged();
    });

    /// <summary>
    /// Called by combobox.js when the mouse actually moves onto another row (not when a row only slides
    /// under a still pointer). Disabled rows are skipped.
    /// </summary>
    [JSInvokable]
    public Task HighlightOptionFromPointer(string optionId) => InvokeAsync(() =>
    {
        if (!IsOpen)
            return;

        var index = ItemsList.FindIndex(item => GetOptionId(item) == optionId);
        if (index < 0 || index == _highlightedIndex || IsItemDisabled(ItemsList[index]))
            return;

        _highlightedIndex = index;
        StateHasChanged();
    });

    private async Task ClearValueAsync()
    {
        if (Disabled)
            return;

        if (IsOpen)
            await CloseDropdownAsync(false);

        if (MultiSelect)
        {
            IReadOnlyList<TItem> empty = [];
            Values = empty;
            await ValuesChanged.InvokeAsync(empty);
        }
        else
        {
            Value = default;
            await ValueChanged.InvokeAsync(default);
        }

        await OnClear.InvokeAsync();

        if (_hasRendered)
            await _triggerRef.FocusAsync();
    }

    private string GetItemClasses(TItem item)
    {
        return new ClassBuilder()
            .AddClass("combo-item")
            .AddClass("highlighted", IsHighlighted(item))
            .AddClass("combo-item-disabled", IsItemDisabled(item))
            .Build();
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
        else if (e.Key == "Enter" && IsOpen)
        {
            if (HasHighlightedItem)
            {
                // Enter also clicks the trigger button, which would toggle the dropdown. On a disabled
                // item nothing happens and the dropdown stays open.
                _skipNextToggle = true;
                await ItemClicked(ItemsList[_highlightedIndex]);
            }
        }
        else if (e.Key == " " && IsOpen && MultiSelect)
        {
            // combobox.js cancels the keydown, so Space does not click the trigger here.
            if (HasHighlightedItem)
                await ItemClicked(ItemsList[_highlightedIndex]);
        }
        else if (e.Key == "Tab" && IsOpen && MultiSelect)
        {
            await CloseDropdownAsync(false);
        }
    }

    /// <summary>
    /// Moves the highlight one row. Disabled items are reachable, so a screen reader announces them
    /// (aria-disabled), but Enter, Space and click do not pick them.
    /// </summary>
    private void MoveHighlight(int offset)
    {
        if (ItemsList.Count == 0)
            return;

        var startIndex = _highlightedIndex < 0 ? (offset > 0 ? 0 : ItemsList.Count - 1) : _highlightedIndex + offset;
        _highlightedIndex = Math.Clamp(startIndex, 0, ItemsList.Count - 1);
    }

    private int FindNextEnabledIndex(int startIndex, int offset)
    {
        if (ItemsList.Count == 0)
            return -1;

        var step = offset < 0 ? -1 : 1;
        for (var index = Math.Clamp(startIndex, 0, ItemsList.Count - 1); index >= 0 && index < ItemsList.Count; index += step)
        {
            if (!IsItemDisabled(ItemsList[index]))
                return index;
        }

        return -1;
    }

    // Set by keyboard navigation; the scroll runs in OnAfterRenderAsync. Calling into JS straight
    // from the key handler would reach the browser before the new highlight has been rendered, so
    // the list would always scroll to the previously highlighted row.
    private bool _scrollToHighlightPending;

    private Task ScrollHighlightedItemIntoView()
    {
        _scrollToHighlightPending = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_scrollToHighlightPending)
            return;

        _scrollToHighlightPending = false;
        if (Js is not null && _rootRef.HasValue && IsOpen)
        {
            try
            {
                var optionId = HasHighlightedItem ? GetOptionId(ItemsList[_highlightedIndex]) : null;
                await Js.InvokeVoidAsync("comboBoxScrollToHighlighted", _rootRef, PortalId, optionId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit closed; nothing to scroll.
            }
        }
    }

    private async Task ItemClicked(TItem item)
    {
        if (IsItemDisabled(item))
            return;

        if (MultiSelect)
        {
            // The dropdown stays open so several items can be toggled in a row.
            var updated = MultiSelection.Toggle(Values, item, ItemEquals);
            Values = updated;
            await ValuesChanged.InvokeAsync(updated);
            return;
        }

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
            try
            {
                await _outsideClickListener.InvokeVoidAsync("dispose");
                await _outsideClickListener.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit closed before teardown; the listener went with it.
            }

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

        // Safety net for a portal node that outlived its component. Skipped unless the
        // component actually rendered interactively: during prerendering there is no JS
        // runtime, and calling into it throws. The circuit can also already be gone.
        if (Js is not null && _hasRendered)
        {
            try
            {
                await Js.InvokeVoidAsync("portalHelper.removeFromBody", PortalId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit closed before teardown; the DOM went with it.
            }
        }
    }
}
