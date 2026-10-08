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
    private string _inputId = $"combo-input-{Guid.NewGuid()}";
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

    /// <summary>
    /// Callback invoked with the current search text when the add option is picked
    /// (by click, by Enter on the highlighted add row, or by Enter with nothing highlighted).
    /// </summary>
    [Parameter]
    public EventCallback<string?> AddOptionChanged { get; set; }

    /// <summary>
    /// Placeholder text displayed when no value is selected.
    /// When <see cref="SearchInTrigger"/> is true it is shown inside the search input.
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

    [Parameter]
    public Func<TItem, bool>? ItemDisabledFunc { get; set; }

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

    /// <summary>
    /// Where the add option row is rendered relative to the results. Defaults to <see cref="AddOptionPosition.Top"/>.
    /// </summary>
    [Parameter]
    public AddOptionPosition AddOptionPosition { get; set; } = AddOptionPosition.Top;

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

    /// <summary>
    /// When true the trigger is a text input: typing opens the dropdown and filters, and the
    /// dropdown has no search input of its own.
    /// </summary>
    [Parameter]
    public bool SearchInTrigger { get; set; }

    /// <summary>
    /// The search text. Bind it with <c>@bind-SearchText</c> to prefill the search term or read
    /// what was typed. Setting it from the parent does not run a search until the user focuses the input.
    /// </summary>
    [Parameter]
    public string? SearchText { get; set; }

    /// <summary>
    /// Invoked with the search text once the user stops typing (after <see cref="SearchDebounceMs"/>),
    /// and immediately when the text is cleared or replaced by a pick.
    /// </summary>
    [Parameter]
    public EventCallback<string?> SearchTextChanged { get; set; }

    /// <summary>
    /// Groups consecutive items by the returned key and renders a header whenever the key changes.
    /// Items must already be sorted by group. Requires <see cref="Virtualize"/> to be false.
    /// </summary>
    [Parameter]
    public Func<TItem, string>? GroupBy { get; set; }

    /// <summary>
    /// Template for a group header. Defaults to the key as uppercase text.
    /// </summary>
    [Parameter]
    public RenderFragment<string>? GroupHeaderTemplate { get; set; }

    /// <summary>
    /// Template rendered after the last item of each group (e.g. a "show more" button).
    /// Clicking inside it does not close the dropdown or move focus away from the input.
    /// </summary>
    [Parameter]
    public RenderFragment<ComboGroupContext<TItem>>? GroupFooterTemplate { get; set; }

    /// <summary>
    /// When false the items are rendered without virtualization, so rows may have variable height.
    /// The data source is then queried once per search with Skip=0 and Take=<see cref="PageSize"/>.
    /// </summary>
    [Parameter]
    public bool Virtualize { get; set; } = true;

    /// <summary>
    /// Number of items requested per search when <see cref="Virtualize"/> is false.
    /// </summary>
    [Parameter]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Invoked on every pick (click or Enter), in addition to <see cref="ValueChanged"/>.
    /// </summary>
    [Parameter]
    public EventCallback<TItem> OnItemSelected { get; set; }

    /// <summary>
    /// When true a pick does not set <see cref="Value"/>: the dropdown closes and the search text is cleared.
    /// Use it together with <see cref="OnItemSelected"/>.
    /// </summary>
    [Parameter]
    public bool ClearOnSelect { get; set; }

    /// <summary>
    /// When true the first enabled item is highlighted after each search. When false nothing is
    /// highlighted until the user presses an arrow key, so Enter submits the add option instead.
    /// </summary>
    [Parameter]
    public bool HighlightFirstItem { get; set; } = true;

    /// <summary>
    /// Static content rendered at the bottom of the dropdown, below the results (e.g. keyboard hints).
    /// </summary>
    [Parameter]
    public RenderFragment? FooterTemplate { get; set; }

    private string PortalId { get; } = $"portal-{Guid.NewGuid()}";


    private string Classes => new ClassBuilder()
        .AddClass("combo-container")
        .AddClass("combo-disabled", Disabled)
        .AddClass(Class)
        .Build();

    private string InputClasses => new ClassBuilder()
        .AddClass("combo-trigger")
        .AddClass("combo-trigger-search", SearchInTrigger)
        .AddClass(InputClass)
        .Build();

    private string DropdownClasses => new ClassBuilder()
        .AddClass("combo-dropdown")
        // Holds the dropdown at a definite height until rows are actually in the DOM, so
        // Virtualize measures the scroll container at its settled size on the first pass.
        // Only for DataProvider: re-measuring a StaticData list costs nothing but local
        // filtering, and holding the height there just leaves whitespace under short lists.
        .AddClass("combo-measuring", Virtualize && DataProvider is not null && !_hasRenderedItems)
        .AddClass(DropdownClass)
        .Build();

    private string AddOptionClasses => new ClassBuilder()
        .AddClass("add-button")
        .AddClass("add-button-bottom", AddOptionPosition == AddOptionPosition.Bottom)
        .AddClass("highlighted", _addOptionHighlighted)
        .Build();

    private string TriggerAriaLabel => string.IsNullOrWhiteSpace(Label) ? Placeholder : Label;

    private ElementReference? _rootRef;
    private ElementReference _triggerRef;
    private ElementReference _inputRef;
    private bool IsLoading { get; set; }
    private IJSObjectReference? _outsideClickListener;
    private DotNetObjectReference<Combobox<TItem>>? _dotNetObjectReference;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _loadCts;
    private string _searchText = "";
    private Virtualize<TItem>? _virtualizeRef;
    private bool IsOpen { get; set; }
    private ElementReference _elementReference;
    private List<TItem> _visibleItems = [];
    private List<ItemGroup> _groups = [];
    private int _highlightedIndex;
    private bool _addOptionHighlighted;
    private int _totalItemCount = -1;
    private bool _skipNextToggle;
    private bool _hasRendered;
    private bool _hasRenderedItems;

    // The SearchText parameter value received last. Only a change in the parameter itself counts
    // as the parent setting the text; a re-render that passes the same value never touches the input.
    private string? _lastSearchTextParameter;
    // The text last emitted through SearchTextChanged. A bound parent passes it straight back as
    // SearchText, and that echo must not overwrite what the user has typed since.
    private string? _lastEmittedSearchText;
    // The text last reported through SearchTextChanged (or received from the parent).
    private string _notifiedSearchText = "";
    private bool _inputFocused;
    private bool _suppressFocusOpen;
    // False from opening until the data source has returned for the first time. Until then the
    // dropdown shows only the loading row: no footers and no "no results" content.
    private bool _hasLoadedOnce;

    private bool ShowNoResults => !IsLoading && _totalItemCount == 0;
    // A loading row in the results area while there is nothing to keep on screen.
    private bool ShowLoadingRow => IsLoading && (!_hasLoadedOnce || _visibleItems.Count == 0);
    // A thin bar at the top of the dropdown while earlier results stay visible.
    private bool ShowProgressBar => IsLoading && !ShowLoadingRow;
    private bool IsAddOptionVisible => ShowAddOption && !string.IsNullOrEmpty(_searchText);
    private bool HasHighlightedItem => !_addOptionHighlighted && _highlightedIndex >= 0 && _highlightedIndex < _visibleItems.Count;
    private string AddOptionId => $"{_listboxId}-add";

    private string? ActiveDescendantId
    {
        get
        {
            if (!IsOpen)
                return null;

            if (_addOptionHighlighted)
                return IsAddOptionVisible ? AddOptionId : null;

            if (!HasHighlightedItem)
                return null;

            return Virtualize ? GetOptionId(_visibleItems[_highlightedIndex]) : GetOptionId(_highlightedIndex);
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            _hasRendered = true;

        // Release the definite height only once rows are on screen. Releasing it in the
        // same render batch that the data arrives in can expose a frame where the scroll
        // container has collapsed back to 0px, which makes Virtualize re-measure and refetch.
        if (IsOpen && !_hasRenderedItems && _visibleItems.Count > 0)
        {
            _hasRenderedItems = true;
            StateHasChanged();
        }
    }

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

        if (PageSize <= 0)
            throw new InvalidOperationException("PageSize must be greater than 0.");

        if (GroupBy is not null && Virtualize)
            throw new InvalidOperationException("GroupBy is only supported when Virtualize is false.");

        if (SearchText != _lastSearchTextParameter)
        {
            _lastSearchTextParameter = SearchText;
            var incoming = SearchText ?? "";

            // The parent changed the text: show it, but do not search until the user focuses the input.
            if (incoming != _lastEmittedSearchText && incoming != _searchText)
            {
                CancelDebounce();
                _searchText = incoming;
                _notifiedSearchText = incoming;
                _lastEmittedSearchText = null;
            }
        }
    }

    /// <summary>
    /// Moves focus to the search input (when <see cref="SearchInTrigger"/> is true) or to the trigger button.
    /// </summary>
    public Task FocusAsync() => InvokeAsync(async () =>
    {
        if (!_hasRendered)
            return;

        if (SearchInTrigger)
            await _inputRef.FocusAsync();
        else
            await _triggerRef.FocusAsync();
    });

    /// <summary>
    /// Reloads the items for the current search text, keeping the dropdown open.
    /// Does nothing while the dropdown is closed; the next open loads fresh data anyway.
    /// </summary>
    public Task RefreshAsync() => InvokeAsync(async () =>
    {
        if (!IsOpen)
            return;

        if (Virtualize)
        {
            if (_virtualizeRef != null)
                await _virtualizeRef.RefreshDataAsync();
        }
        else
        {
            await LoadPageAsync(preserveHighlight: true);
        }
    });

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
        await CancelPendingSearchAsync();

        IsOpen = true;
        // The trigger input keeps whatever was typed; the in-dropdown search starts empty
        // unless the parent prefilled it through SearchText.
        if (!SearchInTrigger)
        {
            _searchText = SearchText ?? "";
            _notifiedSearchText = _searchText;
        }

        _totalItemCount = -1;
        _hasLoadedOnce = false;
        ResetHighlight();

        if (!SearchInTrigger)
            _ = Task.Delay(10).ContinueWith(_ => { InvokeAsync(() => _elementReference.FocusAsync()); });

        if (!Virtualize)
        {
            _visibleItems = [];
            _groups = [];
        }

        _dotNetObjectReference = DotNetObjectReference.Create(this);
        if (Js != null)
        {
            _outsideClickListener = await Js.InvokeAsync<IJSObjectReference>(
                "comboBoxRegisterOutsideClick",
                _rootRef,
                _dotNetObjectReference,
                PortalId,
                SearchInTrigger
            );
        }

        if (!Virtualize)
            await LoadPageAsync(preserveHighlight: false);
    }

    private async Task CloseDropdownAsync(bool returnFocusToTrigger)
    {
        await CancelPendingSearchAsync();

        if (!IsOpen)
            return;

        IsOpen = false;
        _hasRenderedItems = false;
        _addOptionHighlighted = false;
        CancelLoad();

        // The Virtualize inside the portal is torn down along with it. Blazor does not
        // clear component ref captures on removal, so drop it here: RefreshDataAsync()
        // is not guarded against a disposed instance and would re-invoke the provider.
        _virtualizeRef = null;

        await RemoveOutsideClickListener();

        if (returnFocusToTrigger)
            await FocusTriggerAsync();
    }

    private async Task FocusTriggerAsync()
    {
        if (!SearchInTrigger)
        {
            await _triggerRef.FocusAsync();
            return;
        }

        // A pick by mouse or keyboard leaves focus in the input, so this only matters when
        // it has moved elsewhere. Getting it back must not reopen the dropdown.
        if (!_inputFocused)
        {
            _suppressFocusOpen = true;
            await _inputRef.FocusAsync();
        }
    }

    private string GetItemString(TItem item)
    {
        if (ToStringFunc != null)
            return ToStringFunc(item);
        return item?.ToString() ?? "";
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString() ?? "";
        _totalItemCount = -1;
        _hasRenderedItems = false;
        ResetHighlight();

        CancelDebounce();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(Math.Max(0, SearchDebounceMs), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await NotifySearchTextAsync();
        if (token.IsCancellationRequested)
            return;

        if (SearchInTrigger && !IsOpen)
        {
            // Typing in the trigger opens the dropdown; opening loads the items itself.
            if (!string.IsNullOrEmpty(_searchText) && !Disabled)
                await OpenDropdownAsync();
            return;
        }

        await RefreshItems();
    }

    private async Task RefreshItems()
    {
        if (!IsOpen)
            return;

        if (Virtualize)
        {
            if (_virtualizeRef != null)
                await _virtualizeRef.RefreshDataAsync();
        }
        else
        {
            await LoadPageAsync(preserveHighlight: false);
        }
    }

    private async Task NotifySearchTextAsync()
    {
        if (_searchText == _notifiedSearchText)
            return;

        _notifiedSearchText = _searchText;

        // Recorded before invoking: a bound parent re-renders inside the callback and passes the
        // value straight back as SearchText, which must not count as a change made by the parent.
        _lastEmittedSearchText = _searchText;

        await SearchTextChanged.InvokeAsync(_searchText);
    }

    /// <summary>
    /// Drops a pending debounced search but still reports the text typed so far.
    /// </summary>
    private async Task CancelPendingSearchAsync()
    {
        CancelDebounce();
        await NotifySearchTextAsync();
    }

    private void CancelDebounce()
    {
        if (_debounceCts == null)
            return;

        _debounceCts.Cancel();
        _debounceCts.Dispose();
        _debounceCts = null;
    }

    private void CancelLoad()
    {
        if (_loadCts == null)
            return;

        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = null;
        IsLoading = false;
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
                    new ComboState(_searchText, request.Count, request.StartIndex),
                    request.CancellationToken
                );

                _visibleItems = result.Items.ToList();
                _totalItemCount = result.TotalItemCount;
                _hasLoadedOnce = true;
                EnsureHighlightedItemIsEnabled();
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
            var filtered = FilterStaticData(_searchText);

            var page = filtered
                .Skip(request.StartIndex)
                .Take(request.Count)
                .ToList();

            _visibleItems = page;
            _totalItemCount = filtered.Count;
            _hasLoadedOnce = true;
            EnsureHighlightedItemIsEnabled();
            return new ItemsProviderResult<TItem>(page, filtered.Count);
        }

        // No data
        _totalItemCount = 0;
        _hasLoadedOnce = true;
        return new ItemsProviderResult<TItem>(Array.Empty<TItem>(), 0);
    }

    /// <summary>
    /// Loads the first <see cref="PageSize"/> items when <see cref="Virtualize"/> is false.
    /// </summary>
    private async Task LoadPageAsync(bool preserveHighlight)
    {
        CancelLoad();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;

        var hadHighlightedItem = preserveHighlight && HasHighlightedItem;
        var previouslyHighlighted = hadHighlightedItem ? _visibleItems[_highlightedIndex] : default;

        IsLoading = true;
        StateHasChanged();

        try
        {
            List<TItem> items;
            if (DataProvider is not null)
            {
                var result = await DataProvider(new ComboState(_searchText, PageSize, 0), token);
                items = result.Items.ToList();
            }
            else
            {
                items = FilterStaticData(_searchText).Take(PageSize).ToList();
            }

            if (token.IsCancellationRequested)
                return;

            _visibleItems = items;
            _groups = BuildGroups(items);
            _totalItemCount = items.Count;
            _hasLoadedOnce = true;

            if (hadHighlightedItem)
                _highlightedIndex = _visibleItems.FindIndex(item => ItemEquals(item, previouslyHighlighted));

            EnsureHighlightedItemIsEnabled();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            // A newer load (or closing the dropdown) owns the loading state now.
            if (_loadCts == cts)
            {
                _loadCts = null;
                cts.Dispose();
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private List<TItem> FilterStaticData(string search)
    {
        return string.IsNullOrEmpty(search)
            ? StaticData!
            : StaticData!.Where(item => ItemMatches(item, search)).ToList();
    }

    private List<ItemGroup> BuildGroups(List<TItem> items)
    {
        if (GroupBy is null)
            return [];

        var groups = new List<ItemGroup>();
        var start = 0;
        for (var index = 1; index <= items.Count; index++)
        {
            if (index < items.Count && GroupBy(items[index]) == GroupBy(items[start]))
                continue;

            var key = GroupBy(items[start]);
            var groupItems = items.GetRange(start, index - start);
            groups.Add(new ItemGroup($"{_listboxId}-group-{groups.Count}", start, new ComboGroupContext<TItem>(key, groupItems)));
            start = index;
        }

        return groups;
    }

    private bool ItemMatches(TItem item, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var str = GetItemString(item);
        return str.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHighlighted(TItem item)
    {
        if (!HasHighlightedItem)
            return false;

        var highlighted = _visibleItems[_highlightedIndex];
        return ItemEquals(highlighted, item);
    }

    private bool IsItemDisabled(TItem item)
    {
        return ItemDisabledFunc?.Invoke(item) == true;
    }

    private string GetItemClasses(TItem item, bool highlighted)
    {
        return new ClassBuilder()
            .AddClass("combo-item")
            .AddClass("highlighted", highlighted)
            .AddClass("combo-item-disabled", IsItemDisabled(item))
            .Build();
    }

    private string GetOptionId(TItem item)
    {
        var itemString = GetItemString(item);
        var hash = unchecked((uint)itemString.GetHashCode(StringComparison.Ordinal));
        return $"{_listboxId}-option-{hash}";
    }

    // Without virtualization every row is rendered, so its position is a stable, unique id
    // (two groups may well contain items with the same text).
    private string GetOptionId(int index) => $"{_listboxId}-option-{index}";

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

        await HandleListKeyDownAsync(e);
    }

    private async Task HandleSearchInputKeyDown(KeyboardEventArgs e)
    {
        if (Disabled)
            return;

        switch (e.Key)
        {
            case "Escape":
                // Focus is already in the input, which is where it should stay.
                await CloseDropdownAsync(false);
                break;
            case "Tab":
                await CloseDropdownAsync(false);
                break;
            case "ArrowDown" or "ArrowUp" when !IsOpen:
                await OpenDropdownAsync();
                break;
            case "Enter" when !IsOpen:
                if (IsAddOptionVisible)
                    await AddClicked(_searchText);
                break;
            default:
                if (IsOpen)
                    await HandleListKeyDownAsync(e);
                break;
        }
    }

    /// <summary>
    /// Arrow and Enter handling shared by the in-dropdown search input and the trigger input.
    /// </summary>
    private async Task HandleListKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "ArrowDown" or "ArrowUp")
        {
            MoveHighlight(e.Key == "ArrowDown" ? 1 : -1);
            StateHasChanged();
            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "Enter")
        {
            await SubmitHighlightedAsync();
        }
    }

    private async Task SubmitHighlightedAsync()
    {
        if (_addOptionHighlighted)
        {
            if (IsAddOptionVisible)
                await SubmitAddOptionAsync();
            return;
        }

        if (HasHighlightedItem)
        {
            var item = _visibleItems[_highlightedIndex];
            if (IsItemDisabled(item))
                return;

            if (!SearchInTrigger)
                _skipNextToggle = true;
            await ItemClicked(item);
            return;
        }

        // Nothing highlighted: Enter submits the typed text.
        if (IsAddOptionVisible)
            await SubmitAddOptionAsync();
    }

    private async Task SubmitAddOptionAsync()
    {
        if (!SearchInTrigger)
            _skipNextToggle = true;
        await AddClicked(_searchText);
    }

    private void ResetHighlight()
    {
        _addOptionHighlighted = false;
        _highlightedIndex = HighlightFirstItem ? 0 : -1;
    }

    /// <summary>
    /// Moves the highlight over enabled items, plus the add option row when it is visible.
    /// Group headers and footers are not part of <see cref="_visibleItems"/>, so they are never highlighted.
    /// </summary>
    private void MoveHighlight(int offset)
    {
        var step = offset < 0 ? -1 : 1;
        var addVisible = IsAddOptionVisible;
        var addOnTop = AddOptionPosition == AddOptionPosition.Top;

        if (_addOptionHighlighted)
        {
            // From the add option, only moving towards the list goes anywhere.
            if (addOnTop == step > 0)
            {
                var next = addOnTop ? FindEnabledIndex(0, 1) : FindEnabledIndex(_visibleItems.Count - 1, -1);
                if (next >= 0)
                {
                    _addOptionHighlighted = false;
                    _highlightedIndex = next;
                }
            }

            return;
        }

        if (!HasHighlightedItem)
        {
            if (addVisible && step < 0 && !addOnTop)
            {
                HighlightAddOption();
                return;
            }

            var first = step > 0 ? FindEnabledIndex(0, 1) : FindEnabledIndex(_visibleItems.Count - 1, -1);
            if (first >= 0)
                _highlightedIndex = first;
            else if (addVisible)
                HighlightAddOption();
            return;
        }

        var nextIndex = FindEnabledIndex(_highlightedIndex + step, step);
        if (nextIndex >= 0)
            _highlightedIndex = nextIndex;
        else if (addVisible && addOnTop == step < 0)
            HighlightAddOption();
    }

    private void HighlightAddOption()
    {
        _addOptionHighlighted = true;
        _highlightedIndex = -1;
    }

    private void EnsureHighlightedItemIsEnabled()
    {
        if (_addOptionHighlighted)
            return;

        if (HasHighlightedItem && !IsItemDisabled(_visibleItems[_highlightedIndex]))
            return;

        _highlightedIndex = HighlightFirstItem ? FindEnabledIndex(0, 1) : -1;
    }

    /// <summary>
    /// Returns the first enabled index from <paramref name="startIndex"/> in the direction of
    /// <paramref name="step"/>, or -1 when the end of the list is reached.
    /// </summary>
    private int FindEnabledIndex(int startIndex, int step)
    {
        for (var index = startIndex; index >= 0 && index < _visibleItems.Count; index += step)
        {
            if (!IsItemDisabled(_visibleItems[index]))
                return index;
        }

        return -1;
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
        if (IsItemDisabled(item))
            return;

        CancelDebounce();

        if (ClearOnSelect)
            _searchText = "";
        else
        {
            Value = item;
            if (SearchInTrigger)
                _searchText = GetItemString(item);
        }

        // Closing reports the new search text through SearchTextChanged.
        await CloseDropdownAsync(true);

        if (!ClearOnSelect)
            await ValueChanged.InvokeAsync(item);

        await OnItemSelected.InvokeAsync(item);
    }

    private async Task AddClicked(string newItem)
    {
        var normalized = string.IsNullOrWhiteSpace(newItem) ? null : newItem.Trim();
        if (string.IsNullOrEmpty(normalized))
            return;

        await AddOptionChanged.InvokeAsync(normalized);
        await CloseDropdownAsync(true);
    }

    private async Task OnSearchInputFocus()
    {
        _inputFocused = true;

        if (_suppressFocusOpen)
        {
            _suppressFocusOpen = false;
            return;
        }

        if (!IsOpen && !Disabled && !string.IsNullOrEmpty(_searchText))
            await OpenDropdownAsync();
    }

    private void OnSearchInputBlur()
    {
        _inputFocused = false;
    }

    private async Task OnSearchInputClick()
    {
        // Reopens after Escape without having to leave the input first.
        if (!IsOpen && _inputFocused && !Disabled && !string.IsNullOrEmpty(_searchText))
            await OpenDropdownAsync();
    }

    private async Task ClearSearch()
    {
        CancelDebounce();
        _searchText = "";
        ResetHighlight();
        await CloseDropdownAsync(false);
        await _inputRef.FocusAsync();
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


    public async ValueTask DisposeAsync()
    {
        CancelDebounce();
        CancelLoad();

        await RemoveOutsideClickListener();

        _dotNetObjectReference = null;
        _outsideClickListener = null;
        _virtualizeRef = null;

        // Safety net for a portal node that outlived its component. Skipped unless the
        // component actually rendered interactively: during prerendering there is no JS
        // runtime, and calling into it throws. The circuit can also already be gone.
        if (Js != null && _hasRendered)
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

    private sealed record ItemGroup(string HeaderId, int StartIndex, ComboGroupContext<TItem> Context);
}
