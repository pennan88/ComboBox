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

## 📋 Features List

- Searchable dropdown
- Virtualized infinite scrolling
- Static or server data sources
- Customizable item and no-results templates
- Selected-item checkmark

## 🏷️ License

MIT © [pennan88](https://github.com/pennan88)
