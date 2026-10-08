# ComboBox for Blazor

A reusable Blazor WebAssembly / Server component: a **virtualized, searchable Combobox (Autocomplete Select)** designed for large datasets. This is a pure C# and Razor component without external UI dependencies.

## ✔️ Purpose

Create a modern dropdown input for Blazor apps that supports:

- Search box inside the dropdown
- Virtualized infinite scrolling
- Static in-memory lists or remote server-side APIs
- Infinite paging with load-more on scroll
- Customizable item templates
- Optional selected-item checkmark
- Customizable "no results" message with access to the search term
- Closes automatically when clicking outside
- Optional "search box mode": type directly in the field, grouped results, Enter submits free text

## ✅ Use Cases

- Selecting from thousands of items
- API-driven search
- Autocomplete UIs

## 🚀 Installation

### Via [NuGet](https://www.nuget.org/packages/ComboBox/)

```sh
dotnet add package ComboBox --version [latest]
```

Or search for `ComboBox` in NuGet Package Manager.

### As a Local Project

1. Clone this repository:
   ```sh
   git clone https://github.com/pennan88/ComboBox.git
   ```

### Add the project reference to your Blazor solution.

```csharp
@using ComboBox.Components
```

```html
<link href="_content/ComboBox/combobox.css" rel="stylesheet" />
<script src="_content/ComboBox/combobox.js"></script>
```


## 🛠️ Usage

### Static List Example

```razor
<Combobox TItem="string"
              StaticData="myList"
              @bind-Value="selected"
              Placeholder="Choose an item..." />
```

```csharp
@code {
    string selected;
    List<string> myList = new() { "Apple", "Banana", "Cherry", ... };
}
```

### API-Driven Example

```razor
  <div style="width: 300px">
    <Combobox TItem="User" DataProvider="@(Search)" Label="Hello world"
         @bind-Value="SelectedUser" Disabled="@disabled" Placeholder="Pick a fruit" ItemSize="32.2f" >
        <SelectedTemplate Context="selected">
            <div style="display: flex; flex-direction: column;">
                <p><strong>@selected.Name</strong> @selected.Last</p>
                <p>@selected.Number</p>
            </div>
        </SelectedTemplate>
        <ItemTemplate Context="item" >
            <div style="display: flex; gap: 4px;">
                <p>@item.Name</p>
                <p>@item.Last</p>
            </div>
        </ItemTemplate>
        <NoResultsTemplate>
            <p>Sadge :/</p>
        </NoResultsTemplate>
    </Combobox>
</div>

```

```csharp
private List<User> Users = [];

async ValueTask<ItemsProviderResult<User>> Search(ComboState state, CancellationToken token)
{
   var allItems = Users;
   await Task.Delay(1000, token);
   
   var filtered = string.IsNullOrWhiteSpace(state.Search)
   ? allItems
   : allItems.Where(x => x.Name!.Contains(state.Search, StringComparison.OrdinalIgnoreCase)).ToList();
   
   var page = filtered.Skip(state.Skip).Take(state.Take).ToList();
   
   return new ItemsProviderResult<User>(page, filtered.Count);
}
```

### Add Option Example

```razor
<Combobox TItem="string"
          StaticData="Tags"
          @bind-Value="SelectedTag"
          ShowAddOption="true"
          AddOptionText="Add"
          AddOptionChanged="OnAddTag" />
```

```csharp
@code {
    private string? SelectedTag;
    private List<string> Tags = ["Blazor", "C#", "Razor"];

    private Task OnAddTag(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !Tags.Contains(value))
        {
            Tags.Add(value);
            SelectedTag = value;
        }

        return Task.CompletedTask;
    }
}
```

### Simple Select Example

```razor
<Select TItem="string"
        Items="Fruits"
        @bind-Value="SelectedFruit"
        Placeholder="Choose a fruit..." />
```

```csharp
@code {
    private string? SelectedFruit;
    private List<string> Fruits = ["Apple", "Banana", "Cherry"];
}
```

Use `<Select>` when you want the same trigger/dropdown styling without search, virtualization, or remote loading.

For server-side options without pagination, use `DataProvider`:

```razor
<Select TItem="User"
        DataProvider="LoadUsers"
        @bind-Value="SelectedUser"
        ToStringFunc="@(user => user.Name)"
        Placeholder="Choose a user..." />
```

```csharp
@code {
    private User? SelectedUser;

    private async ValueTask<IEnumerable<User>> LoadUsers(CancellationToken cancellationToken)
    {
        return await UserClient.GetUsersAsync(cancellationToken);
    }
}
```

### Custom Equality Example

```razor
<Combobox TItem="User"
          StaticData="Users"
          @bind-Value="SelectedUser"
          EqualityComparer="UserIdComparer.Instance"
          ToStringFunc="@(u => $\"{u.FirstName} {u.LastName}\")" />
```

```csharp
public sealed class UserIdComparer : IEqualityComparer<User>
{
    public static UserIdComparer Instance { get; } = new();

    public bool Equals(User? x, User? y) => x?.Id == y?.Id;
    public int GetHashCode(User obj) => obj.Id.GetHashCode();
}
```

### Text Customization Example

```razor
<Combobox TItem="string"
          StaticData="myList"
          SearchPlaceholder="Search products..."
          NoResultsText="No matching products"
          AddOptionText="Create" />
```

### Search Box Mode

A Jira-style search box: the trigger itself is the text input, results are grouped with a
header and footer per group, picking an item does not keep a value, and Enter with nothing
highlighted submits the typed text.

```razor
<Combobox @ref="_search"
          TItem="SearchResult"
          DataProvider="Search"
          SearchInTrigger="true"
          Virtualize="false"
          @bind-SearchText="Text"
          GroupBy="@(x => x.Type)"
          HighlightFirstItem="false"
          ShowAddOption="true"
          AddOptionPosition="AddOptionPosition.Top"
          AddOptionText="Search for:"
          AddOptionChanged="SubmitText"
          OnItemSelected="Pick"
          ClearOnSelect="true"
          DropdownWidth="200px"
          Placeholder="Search projects, users or groups">
    <GroupHeaderTemplate Context="key">@key</GroupHeaderTemplate>
    <ItemTemplate Context="item">
        <div><strong>@item.Name</strong><div>@item.Details</div></div>
    </ItemTemplate>
    <GroupFooterTemplate Context="g"><button @onclick="() => More(g.Key)">Show more</button></GroupFooterTemplate>
    <FooterTemplate>↑↓ navigate · Enter selects · Esc closes</FooterTemplate>
</Combobox>
```

```csharp
@code {
    private Combobox<SearchResult>? _search;
    private string Text { get; set; } = "";
    private readonly Dictionary<string, int> _takePerGroup = new();

    // Items must come back sorted by group: a header is rendered whenever the key changes.
    private async ValueTask<ItemsProviderResult<SearchResult>> Search(ComboState state, CancellationToken token)
    {
        var items = (await Api.SearchAsync(state.Search, token))
            .GroupBy(x => x.Type)
            .OrderBy(g => g.Key)
            .SelectMany(g => g.Take(_takePerGroup.GetValueOrDefault(g.Key, 3)))
            .ToList();

        return new ItemsProviderResult<SearchResult>(items, items.Count);
    }

    private void Pick(SearchResult item) => Navigation.NavigateTo(item.Url);

    private void SubmitText(string? text) => Navigation.NavigateTo($"/search?q={Uri.EscapeDataString(text ?? "")}");

    // The footer button does not close the dropdown; RefreshAsync reloads it in place.
    private async Task More(string group)
    {
        _takePerGroup[group] = _takePerGroup.GetValueOrDefault(group, 3) + 5;
        await _search!.RefreshAsync();
    }
}
```

Notes:

- With `SearchInTrigger="true"` typing opens the dropdown after `SearchDebounceMs`. Focusing the
  input while it holds text (for example a filter restored through `SearchText`) also opens it.
  Setting `SearchText` from the parent never runs a search on its own.
- `GroupBy` is only supported together with `Virtualize="false"`, so rows can have any height.
  The data source is then called once per search with `Skip = 0` and `Take = PageSize`.
- `DropdownWidth` is added to the width of the input, so `200px` on a 400px input gives a 600px dropdown.

**Loading:** while the first results for an open dropdown are loading, the results area shows only a
loading row. `FooterTemplate`, `GroupFooterTemplate` and `NoResultsTemplate` appear once the data source
has returned. Later reloads (typing more, `RefreshAsync()`) keep the current results on screen and show a
thin progress bar at the top of the dropdown instead. Screen readers hear "Loading results" either way.

## 🎨 Styling Guidance

- Uses semantic HTML and CSS classes.
- Override or extend styles with your own CSS:
  ```css
  .combobox-dropdown {
    background: #f9f9f9;
  }
  .combobox-item.selected {
    font-weight: bold;
  }
  ```

## 📚 API Documentation

### `<ComboBox TValue="TItem">` Parameters

| Parameter                | Type                                               | Description                                                                                                              |
|--------------------------|----------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------|
| `Value`                  | `TItem?`                                           | The currently selected value.                                                                                            |
| `ValueChanged`           | `EventCallback<TItem?>`                            | Callback invoked when the selected value changes.                                                                        |
| `AddOptionChanged`       | `EventCallback<string?>`                           | Callback invoked when the add-option action is clicked (required when `ShowAddOption` is `true`).                      |
| `Placeholder`            | `string`                                           | Placeholder text displayed when no value is selected.                                                                    |
| `SearchPlaceholder`      | `string` (default: `"Search..."`)                | Placeholder text displayed in the search input.                                                                          |
| `NoResultsText`          | `string` (default: `"No results"`)               | Default no-results text when `NoResultsTemplate` is not provided.                                                       |
| `AddOptionText`          | `string` (default: `"Add"`)                      | Label text shown for the add-option action row.                                                                          |
| `AdornmentIcon`          | `string?`                                          | Custom trigger icon. Accepts SVG path (`d`) strings (e.g. `Icons.Material.Filled.ExpandMore`) or raw SVG markup.       |
| `ToStringFunc`           | `Func<TItem, string>?`                             | Converts an item to its string representation for display.                                                               |
| `EqualityComparer`       | `IEqualityComparer<TItem>?`                        | Custom comparer used to determine if an item is selected/highlighted.                                                   |
| `ShowCheckmarkInList`    | `bool` (default: `true`)                           | Whether to show a checkmark next to selected items.                                                                      |
| `DataProvider`           | `Func<ComboState, CancellationToken, ValueTask<ItemsProviderResult<TItem>>>?` | Async provider for paged/filtered data (for large or remote lists).                                                      |
| `StaticData`             | `List<TItem>?`                                     | Static list of items to display (for simple usage).                                                                      |
| `SearchDebounceMs`       | `int` (default: `300`)                             | Debounce duration in milliseconds before refreshing search results.                                                      |
| `ItemTemplate`           | `RenderFragment<TItem>?`                           | Template for rendering each item in the dropdown list.                                                                   |
| `SelectedTemplate`       | `RenderFragment<TItem>?`                           | Template for rendering the selected item when the dropdown is closed.                                                    |
| `NoResultsTemplate`      | `RenderFragment?`                                  | Template displayed when no matching items are found.                                                                     |
| `Disabled`               | `bool`                                             | Whether the component is disabled.                                                                                       |
| `Class`                  | `string?`                                          | CSS class applied to the root element.                                                                                   |
| `InputClass`             | `string?`                                          | CSS class applied to the input element.                                                                                  |
| `DropdownClass`          | `string?`                                          | CSS class applied to the dropdown container.                                                                             |
| `DropdownWidth`          | `string?`                                          | Additional CSS width added to trigger width (for example: `"120px"`, `"8rem"`). Defaults to `0` when omitted.      |
| `AnchorPosition`         | `DropdownAnchorPosition` (default: `Left`)         | Controls expansion anchor for dropdown width: `Left` (expand right), `Center` (expand both sides), `Right` (expand left). |
| `Label`                  | `string?`                                          | Optional label text for the component.                                                                                   |
| `ItemSize`               | `float` (default: `50f`)                           | Fixed pixel height of each item for virtualization.                                                                      |
| `SearchInTrigger`        | `bool` (default: `false`)                          | Makes the trigger a text input: typing opens and filters the dropdown, which then has no search input of its own. Shows `Placeholder` and a clear (×) button. |
| `SearchText`             | `string?`                                          | Search text, bindable with `@bind-SearchText`. Prefills the search; changing it from the parent does not search until the input is focused. Only a change in the parameter value is applied: re-rendering with the same value, or with the value just emitted through `SearchTextChanged`, never touches the input. |
| `SearchTextChanged`      | `EventCallback<string?>`                           | Invoked with the typed text after `SearchDebounceMs`, and immediately when the text is cleared or replaced by a pick.     |
| `GroupBy`                | `Func<TItem, string>?`                             | Groups consecutive items by key and renders a header whenever the key changes. Items must be sorted by group. Requires `Virtualize="false"`. |
| `GroupHeaderTemplate`    | `RenderFragment<string>?`                          | Template for a group header. Default: the key as uppercase text.                                                         |
| `GroupFooterTemplate`    | `RenderFragment<ComboGroupContext<TItem>>?`        | Template after each group, with `Key` and `Items`. Clicks inside it keep the dropdown open and the focus in the input.   |
| `Virtualize`             | `bool` (default: `true`)                           | `false` renders the items without virtualization (variable row height) and loads `PageSize` items per search.            |
| `PageSize`               | `int` (default: `50`)                              | Number of items requested per search when `Virtualize` is `false`.                                                       |
| `OnItemSelected`         | `EventCallback<TItem>`                             | Invoked on every pick (click or Enter), in addition to `ValueChanged`.                                                   |
| `ClearOnSelect`          | `bool` (default: `false`)                          | A pick does not set `Value`: the dropdown closes and the search text is cleared. When `false` with `SearchInTrigger`, a pick puts `ToStringFunc(item)` in the input. |
| `AddOptionPosition`      | `AddOptionPosition` (default: `Top`)               | Places the add option above (`Top`) or below (`Bottom`) the results.                                                     |
| `HighlightFirstItem`     | `bool` (default: `true`)                           | `false` leaves nothing highlighted after a search, so Enter submits the add option until an arrow key is pressed.        |
| `FooterTemplate`         | `RenderFragment?`                                  | Static content at the bottom of the dropdown, below the results (for example keyboard hints).                           |

### Public Methods

| Method           | Description                                                                                         |
|------------------|-----------------------------------------------------------------------------------------------------|
| `FocusAsync()`   | Focuses the search input (`SearchInTrigger`) or the trigger button.                                 |
| `RefreshAsync()` | Calls the data source again with the current search text and keeps the dropdown open. No-op while closed. |

## ⚙️ Configuration Rules

- Set exactly one data source: either `DataProvider` or `StaticData`.
- `ShowAddOption="true"` requires an `AddOptionChanged` callback.
- `SearchDebounceMs` must be `0` or greater.
- `GroupBy` requires `Virtualize="false"`.
- `PageSize` must be greater than `0`.

## ⌨️ Keyboard & Accessibility

- Trigger supports `ArrowDown` / `ArrowUp` to open the dropdown and `Escape` to close.
- Search input supports arrow navigation, `Enter` to select highlighted item, and `Escape` to close.
- With `SearchInTrigger`, the same keys work in the trigger input; `Escape` closes the dropdown and leaves focus in the input, and `Tab` closes it and moves on.
- Arrow keys skip group headers and footers. The add option row is reachable too: `ArrowUp` from the first item when it is on `Top`, `ArrowDown` from the last item when it is at the `Bottom`.
- With `ShowAddOption`, `Enter` with nothing highlighted invokes `AddOptionChanged` with the current search text.
- ARIA roles and attributes are included for combobox semantics (`combobox`, `listbox`, `option`, `aria-expanded`, `aria-controls`, `aria-activedescendant`).
- A polite `aria-live` region announces loading and no-results state.





## 📋 Features List

- Searchable dropdown
- Virtualized infinite scrolling
- Static or server data sources
- Customizable item and no-results templates
- Search box mode with grouped results, per-group footers and free-text submit
- Selected-item checkmark

## 🏷️ License

MIT © [pennan88](https://github.com/pennan88)
