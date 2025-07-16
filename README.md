
# 🌟 VirtualizedCombobox

A **Blazor WebAssembly** component for large, searchable, virtualized select lists with autocomplete support.

Designed for modern UI needs—like a "combobox" (shadcn/ui, MudBlazor Combobox):

- Search input inside the dropdown
- Virtualized infinite scroll
- Works with static lists *or* server APIs
- Supports checkmark for selected item
- Fully templated
- Customizable “no results” message with search term context
- Closes when clicking outside

---

## ✨ Features

✅ Searchable autocomplete input in dropdown  
✅ Virtualized scrolling for large datasets  
✅ Supports local static lists and remote paged APIs  
✅ Optional checkmark for selected item in the dropdown  
✅ Fully customizable item templates  
✅ Custom "no results" message with search term context  
✅ Closes on outside click  

---

## 🚀 How It Works

Supports two main data modes:

### 1️⃣ Static Data
Use `StaticData` to provide a local list (filtering/paging in-memory).

```razor
<VirtualizedCombobox TItem="string"
                     StaticData="MyItems"
                     OnSelected="HandleSelected"
                     Placeholder="Välj ett alternativ">
    <ItemTemplate Context="item">
        <div>@item</div>
    </ItemTemplate>
</VirtualizedCombobox>
```

### 2️⃣ Remote / Server-Side Data
Use DataProvider to support server paging / filtering.

```razor
<VirtualizedCombobox TItem="ProjectDto"
                     DataProvider="LoadProjects"
                     OnSelected="HandleSelectedProject"
                     Placeholder="Välj ett projekt">
    <ItemTemplate Context="item">
        <div>@item.Name (ID: @item.Id)</div>
    </ItemTemplate>
</VirtualizedCombobox>
```


### ⚡ Example With Fake API
Full page example: