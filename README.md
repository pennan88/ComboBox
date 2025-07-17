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
<ComboBox TValue="string"
          Items="myList"
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
<ComboBox TValue="Product"
          ItemsProvider="SearchProductsAsync"
          ItemTemplate="@(product => @<div>@product.Name (@product.Id)</div>)"
          @bind-Value="selectedProduct" />
```

```csharp
@code {
    Product selectedProduct;

    async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, int skip, int take)
    {
        // Fetch from API or database
    }
}
```

### Customizing Templates

```razor
<ComboBox TValue="User"
          Items="users">

  <ItemTemplate Context="Item">
    <p>@item.Name</p>
  </ItemTemplate>

  <NoResultsTemplate>
    <p>Custom no result</p>
  </NoResultsTemplate>
</ComboBox>
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
