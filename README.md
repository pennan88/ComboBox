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
- Optional multi-select: pick several items, the dropdown stays open between picks

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

### Multi-Select

Both `<Select>` and `<Combobox>` take `MultiSelect="true"`. Bind `Values` instead of `Value`: a pick
toggles the item in or out of the list, and the dropdown stays open for the next one.

```razor
<Select TItem="string"
        Items="Fruits"
        MultiSelect="true"
        @bind-Values="SelectedFruits"
        Placeholder="Choose fruits..." />

<Combobox TItem="User"
          DataProvider="LoadUsers"
          MultiSelect="true"
          @bind-Values="SelectedUsers"
          ToStringFunc="@(u => u.Name)"
          MultiSelectionTextFunc="@(users => users.Count == 1 ? users[0].Name : $"{users.Count} users")"
          Placeholder="Choose users..." />
```

```csharp
@code {
    private List<string> Fruits = ["Apple", "Banana", "Cherry"];
    private IReadOnlyList<string> SelectedFruits = [];
    private IReadOnlyList<User> SelectedUsers = [];
}
```

Notes:

- `Values` keeps the order the items were picked in. `ValuesChanged` gets a new list on every toggle.
- The trigger shows `Placeholder` while nothing is selected, otherwise `MultiSelectionTextFunc`
  (default: the items joined with `", "` through `ToStringFunc`) on one line with an ellipsis.
  `SelectedTemplate` is not used.
- Checkmarks and `aria-selected` come from `Values`, so they are right on every page a
  `DataProvider` returns, also with virtualization.
- The dropdown closes on `Escape`, `Tab` or a click outside it.
- `Combobox`: the search works as usual and keeps focus after a pick. `ClearOnSelect="true"` clears the
  search text after each pick but still toggles `Values`. `OnItemSelected` fires on every toggle, in or out.
- `MultiSelect` cannot be combined with `SearchInTrigger`.

### Rich Rows: `ItemContentTemplate`

`ItemContentTemplate` gets a `ComboItemContext<TItem>` with `Item`, `IsSelected`, `IsHighlighted`,
`IsDisabled` and `SearchText` (the search the shown results were loaded for). It takes precedence over
`ItemTemplate`, which keeps working unchanged. `SplitByMatch(text)` splits a text around the search
matches so they can be marked. With `Virtualize="true"` the row keeps the fixed height `ItemSize`.

```razor
<Combobox TItem="User" DataProvider="LoadUsers" MultiSelect="true" @bind-Values="SelectedUsers"
          ShowCheckmarkInList="false" ToStringFunc="@(u => u.Name)">
    <ItemContentTemplate Context="row">
        <input type="checkbox" checked="@row.IsSelected" disabled="@row.IsDisabled" tabindex="-1" aria-hidden="true" />
        <span>
            @foreach (var part in row.SplitByMatch(row.Item.Name))
            {
                @if (part.IsMatch) { <mark>@part.Text</mark> } else { @part.Text }
            }
            <small>@row.Item.Email</small>
        </span>
    </ItemContentTemplate>
</Combobox>
```

With `ShowCheckmarkInList="false"` and `ItemContentTemplate` no checkmark column is reserved, so the
template owns the whole row (for example its own checkbox).

### Clear Button, Loading Spinner and Start Content

```razor
<Select TItem="User" Items="Users" @bind-Value="Owner" ToStringFunc="@(u => u.Name)"
        Clearable="true" ClearButtonAriaLabel="Rensa val" OnClear="OwnerCleared"
        Loading="@savingOwner">
    <StartContent><img class="avatar" src="@Owner?.AvatarUrl" alt="" /></StartContent>
</Select>
```

- `Clearable` shows an × button (16 px) 8 px left of the chevron while there is a value (`Value` or
  `Values`). It is a sibling of the trigger button, not nested in it: both sit in
  `.combo-trigger-wrapper`, and the clear button is laid over a `.combo-clear-spacer` inside the trigger.
  A click resets `Value`/`Values`, invokes `OnClear` and focuses the trigger. If you change the trigger's
  right padding or the chevron size, set `--combo-clear-offset` (default `36px` = 12 px padding + 16 px chevron + 8 px).
  With `SearchInTrigger` the existing × in the input is used; with `Clearable` it also resets `Value`.
- `Loading` adds a 16 px spinner (`.combo-spinner`) next to the chevron and `aria-busy="true"`. The
  chevron stays and the trigger stays usable.
- `StartContent` is rendered first in the trigger (`.combo-start-content`, 8 px to the text), also when
  nothing is selected. It sits inside a `<button>`, so it must not contain interactive elements.

### Closing From a Footer: `CloseAsync()`

```razor
<Combobox @ref="_users" TItem="User" DataProvider="LoadUsers" MultiSelect="true" @bind-Values="SelectedUsers">
    <FooterTemplate>
        <button type="button" @onclick="() => _users!.CloseAsync()">Klar</button>
    </FooterTemplate>
</Combobox>
```

`CloseAsync()` closes the dropdown and returns focus to the trigger. `<Select>` has `FooterTemplate` and `CloseAsync()` too.

### Errors From the DataProvider

When the `DataProvider` throws, the dropdown shows `ErrorTemplate` (or a row with `ErrorText`) instead of
the results, and the error is announced. The context has the `Exception` and `Retry`, which calls
`RefreshAsync()`. A cancellation of the component's own token (a newer search, closing) is not an error.
An `OperationCanceledException` on a token the component did *not* cancel, such as an `HttpClient`
timeout, is shown as an error.

```razor
<Combobox TItem="User" DataProvider="LoadUsers" @bind-Value="SelectedUser" ErrorText="Kunde inte hämta användare.">
    <ErrorTemplate Context="error">
        <div class="error-row">
            Kunde inte hämta användare.
            <button type="button" @onclick="error.Retry">Försök igen</button>
        </div>
    </ErrorTemplate>
</Combobox>
```

### Localized Texts

```razor
<Combobox TItem="User" DataProvider="LoadUsers" @bind-Value="SelectedUser"
          SearchPlaceholder="Sök ..."
          LoadingText="Söker ..."
          LoadingAnnouncementText="Laddar resultat"
          NoResultsText="Inga träffar"
          ErrorText="Något gick fel"
          ResultCountTextFunc="@(n => n == 1 ? "1 träff" : $"{n} träffar")" />
```

- `LoadingText` replaces the progress dots in the first-load row with a spinner and the text.
  `LoadingTemplate` replaces the row's content entirely. Without either the row shows the dots as before.
- The polite live region announces `LoadingAnnouncementText`, `ErrorText`, `NoResultsText`, or the result
  count from `ResultCountTextFunc` (default `"1 result"` / `"{n} results"`). The count is never shown visually.

### External Label and Field Descriptions

```razor
<label id="owner-label" class="adapt-label">Ägare <span>(obligatoriskt)</span></label>
<button type="button" class="info" aria-label="Mer information">i</button>
<Combobox TItem="User" StaticData="Users" @bind-Value="Owner"
          AriaLabelledBy="owner-label"
          AriaDescribedBy="owner-help owner-error"
          Invalid="@(!ownerIsValid)" />
<p id="owner-help">Den som ansvarar för ärendet.</p>
<p id="owner-error">Välj en ägare.</p>
```

The component takes `AriaLabelledBy` rather than a label template: the wrapper keeps full control over the
label markup and layout, and an info button stays outside the `<label>` (interactive content inside a
label is invalid and would end up in the accessible name). `AriaLabelledBy` is set on the trigger and the
listbox and replaces the generated `aria-label`. `AriaDescribedBy` goes on the trigger and on the search
input in the dropdown. `Invalid` sets `aria-invalid="true"` on the trigger and `.combo-invalid` on the root.

### Custom Filtering for `StaticData`

```razor
<Combobox TItem="User" StaticData="Users" @bind-Value="Owner" ToStringFunc="@(u => u.Name)"
          FilterFunc="@((u, search) => u.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                                    || u.Email.Contains(search, StringComparison.CurrentCultureIgnoreCase))" />
```

`FilterFunc` is called with each item and the (non-empty) search text. Without it the search matches
`ToStringFunc` case-insensitively anywhere in the text.

### Dropdown Width Limits

```razor
<Select TItem="string" Items="Fruits" @bind-Value="Fruit"
        DropdownMinWidth="300px" DropdownMaxWidth="480px" AnchorPosition="DropdownAnchorPosition.Right" />
```

The dropdown is at least as wide as the trigger, at least `DropdownMinWidth` and at most `DropdownMaxWidth`
(but never narrower than the trigger). When either is set, the width is resolved in pixels and the dropdown
is moved horizontally to stay 8 px inside the viewport, whatever `AnchorPosition` says. Without them the
placement is exactly as before.

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
| `MultiSelect`            | `bool` (default: `false`)                          | Lets several items be selected through `Values`. A pick toggles the item and the dropdown stays open. Also on `<Select>`. |
| `Values`                 | `IReadOnlyList<TItem>?`                            | The selected items when `MultiSelect` is `true`, in pick order. Bindable with `@bind-Values`.                            |
| `ValuesChanged`          | `EventCallback<IReadOnlyList<TItem>>`              | Invoked with a new list each time an item is toggled.                                                                    |
| `MultiSelectionTextFunc` | `Func<IReadOnlyList<TItem>, string>?`              | Trigger text when something is selected. Default: the items joined with `", "` through `ToStringFunc`.                   |
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
| `OnItemSelected`         | `EventCallback<TItem>`                             | Invoked on every pick (click or Enter), in addition to `ValueChanged`. With `MultiSelect`, on every toggle.              |
| `ClearOnSelect`          | `bool` (default: `false`)                          | A pick does not set `Value`: the dropdown closes and the search text is cleared. When `false` with `SearchInTrigger`, a pick puts `ToStringFunc(item)` in the input. With `MultiSelect` a pick still toggles `Values` and the dropdown stays open; only the search text is cleared. |
| `AddOptionPosition`      | `AddOptionPosition` (default: `Top`)               | Places the add option above (`Top`) or below (`Bottom`) the results.                                                     |
| `HighlightFirstItem`     | `bool` (default: `true`)                           | `false` leaves nothing highlighted after a search, so Enter submits the add option until an arrow key is pressed.        |
| `FooterTemplate`         | `RenderFragment?`                                  | Static content at the bottom of the dropdown, below the results (for example keyboard hints or a "Done" button). Also on `<Select>`. |
| `ItemContentTemplate`    | `RenderFragment<ComboItemContext<TItem>>?`         | Row content with `Item`, `IsSelected`, `IsHighlighted`, `IsDisabled`, `SearchText` and `SplitByMatch(text)`. Wins over `ItemTemplate`. Also on `<Select>` (`SearchText` is empty). |
| `Clearable`              | `bool` (default: `false`)                          | × button left of the chevron while there is a value; resets `Value`/`Values`, invokes `OnClear`, focuses the trigger. Also on `<Select>`. |
| `ClearButtonAriaLabel`   | `string` (default: `"Clear selection"`)            | Accessible name of the clear button. Also on `<Select>`.                                                                  |
| `OnClear`                | `EventCallback`                                    | Invoked after the clear button reset the value. Also on `<Select>`.                                                       |
| `Loading`                | `bool` (default: `false`)                          | 16 px spinner next to the chevron and `aria-busy="true"`; the trigger stays usable. Also on `<Select>`.                  |
| `StartContent`           | `RenderFragment?`                                  | Leading content in the trigger (icon, avatar), shown also without a value. No interactive content. Also on `<Select>`.   |
| `ErrorTemplate`          | `RenderFragment<ComboErrorContext>?`               | Shown when the `DataProvider` throws; context has `Exception` and `Retry` (calls `RefreshAsync()`). Also on `<Select>`.   |
| `ErrorText`              | `string` (default: `"Something went wrong."`)      | Default error row and its announcement. Also on `<Select>`.                                                               |
| `LoadingText`            | `string?` (default: none; `<Select>`: `"Loading..."`) | Combobox: spinner + this text in the first-load row instead of the dots. Select: the existing loading text.          |
| `LoadingTemplate`        | `RenderFragment?`                                  | Replaces the content of the loading row. Also on `<Select>`.                                                              |
| `LoadingAnnouncementText`| `string` (default: `"Loading results"`)            | Announced in the live region while loading. Also on `<Select>`.                                                           |
| `ResultCountTextFunc`    | `Func<int, string>?`                               | Builds the announced (not shown) result count. Default `"1 result"` / `"{n} results"`.                                    |
| `AriaLabelledBy`         | `string?`                                          | Id(s) of an external label, set on the trigger and the listbox; replaces the generated `aria-label`. Also on `<Select>`. |
| `AriaDescribedBy`        | `string?`                                          | Id(s) of help/error text, set on the trigger and on the dropdown search input. Also on `<Select>` (trigger).             |
| `Invalid`                | `bool` (default: `false`)                          | `aria-invalid="true"` on the trigger and `.combo-invalid` on the root. Also on `<Select>`.                                |
| `FilterFunc`             | `Func<TItem, string, bool>?`                       | Search predicate for `StaticData` (item, non-empty search). Default: case-insensitive contains on `ToStringFunc`.        |
| `DropdownMinWidth`       | `string?`                                          | CSS length. Minimum dropdown width; never narrower than the trigger. Keeps the dropdown inside the viewport. Also on `<Select>`. |
| `DropdownMaxWidth`       | `string?`                                          | CSS length. Maximum dropdown width, but not below the trigger width. Keeps the dropdown inside the viewport. Also on `<Select>`. |

### Public Methods

| Method           | Description                                                                                         |
|------------------|-----------------------------------------------------------------------------------------------------|
| `FocusAsync()`   | Focuses the search input (`SearchInTrigger`) or the trigger button.                                 |
| `RefreshAsync()` | Calls the data source again with the current search text and keeps the dropdown open. No-op while closed. `<Select>`: reloads `DataProvider`. |
| `CloseAsync()`   | Closes the dropdown and focuses the trigger, e.g. from a "Done" button in `FooterTemplate`. No-op while closed. Also on `<Select>`. |

## ⚙️ Configuration Rules

- Set exactly one data source: either `DataProvider` or `StaticData`.
- `ShowAddOption="true"` requires an `AddOptionChanged` callback.
- `SearchDebounceMs` must be `0` or greater.
- `GroupBy` requires `Virtualize="false"`.
- `PageSize` must be greater than `0`.
- `MultiSelect` cannot be combined with `SearchInTrigger`.

## ⌨️ Keyboard & Accessibility

- Trigger supports `ArrowDown` / `ArrowUp` to open the dropdown and `Escape` to close.
- Search input supports arrow navigation, `Enter` to select highlighted item, and `Escape` to close.
  With text in the search, the first `Escape` empties the search and the next one closes.
- After a pick in single-select mode the search is empty the next time the dropdown opens (also when
  `SearchText` is bound). In multi-select mode the search stays, so more matches can be picked.
- After each search the first enabled match is active (`HighlightFirstItem`), so `Enter` picks it.
- Disabled items (`ItemDisabledFunc`) are reachable with the arrow keys and carry `aria-disabled="true"`,
  so their reason is read out, but `Enter`, `Space` and clicks do not pick them.
- The mouse makes a row active only when it actually moves over it (mousemove), never when a row
  scrolls under a still pointer. There is no `:hover` styling on rows; style `.combo-item.highlighted`.
- With `SearchInTrigger`, the same keys work in the trigger input; `Escape` closes the dropdown and leaves focus in the input, and `Tab` closes it and moves on.
- Arrow keys skip group headers and footers. The add option row is reachable too: `ArrowUp` from the first item when it is on `Top`, `ArrowDown` from the last item when it is at the `Bottom`.
- With `ShowAddOption`, `Enter` with nothing highlighted invokes `AddOptionChanged` with the current search text.
- With `MultiSelect`, `Enter` and `Space` toggle the highlighted item without closing; `Escape` closes and returns focus to the trigger, and `Tab` closes too. In the `Combobox` search input `Space` toggles only while the search is empty; once you have typed, it is part of the search text.
- ARIA roles and attributes are included for combobox semantics (`combobox`, `listbox`, `option`, `aria-expanded`, `aria-controls`, `aria-activedescendant`, `aria-selected`, and `aria-multiselectable` with `MultiSelect`).
- A polite `aria-live` region announces loading (`LoadingAnnouncementText`), errors (`ErrorText`),
  no results (`NoResultsText`) and the result count (`ResultCountTextFunc`). The count is not shown visually.
- `AriaLabelledBy`, `AriaDescribedBy` and `Invalid` set `aria-labelledby`, `aria-describedby` and
  `aria-invalid` on the trigger (and `aria-describedby` on the dropdown search input). `Loading` sets `aria-busy`.
- The `Clearable` button is a real `<button>` next to the trigger (not inside it) with `ClearButtonAriaLabel`,
  reachable with `Tab`.





## 📋 Features List

- Searchable dropdown
- Virtualized infinite scrolling
- Static or server data sources
- Customizable item and no-results templates
- Search box mode with grouped results, per-group footers and free-text submit
- Multi-select with `@bind-Values` on both `Select` and `Combobox`
- Selected-item checkmark

## 🏷️ License

MIT © [pennan88](https://github.com/pennan88)
