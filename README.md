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
| `Label`                  | `string?`                                          | Optional label text for the component.                                                                                   |
| `ItemSize`               | `float` (default: `50f`)                           | Fixed pixel height of each item for virtualization.                                                                      |

## ⚙️ Configuration Rules

- Set exactly one data source: either `DataProvider` or `StaticData`.
- `ShowAddOption="true"` requires an `AddOptionChanged` callback.
- `SearchDebounceMs` must be `0` or greater.

## ⌨️ Keyboard & Accessibility

- Trigger supports `ArrowDown` / `ArrowUp` to open the dropdown and `Escape` to close.
- Search input supports arrow navigation, `Enter` to select highlighted item, and `Escape` to close.
- ARIA roles and attributes are included for combobox semantics (`combobox`, `listbox`, `option`, `aria-expanded`, `aria-controls`, `aria-activedescendant`).
- A polite `aria-live` region announces loading and no-results state.





## 📋 Features List

- Searchable dropdown
- Virtualized infinite scrolling
- Static or server data sources
- Customizable item and no-results templates
- Selected-item checkmark

## 🏷️ License

MIT © [pennan88](https://github.com/pennan88)
