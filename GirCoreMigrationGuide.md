# Gir.Core Migration Guide for XDM

Migrating XDM from `GtkSharp` to `Gir.Core` is recommended for future-proofing the application, particularly for better compatibility with `.NET 8` AOT compilation, native Wayland rendering without X11 wrappers, and proper memory management without relying on aggressive reflection (which breaks under IL Trimming).

However, because `Gir.Core` and `GtkSharp` have substantially different API surfaces, the migration is essentially a complete UI layer rewrite.

## Migration Steps

### 1. Update Project Dependencies
In `XDM.Gtk.UI.csproj`, replace `GtkSharp` with `GirCore.Gtk-3.0`:
```xml
<ItemGroup>
  <PackageReference Include="GirCore.Gtk-3.0" Version="0.5.0" />
</ItemGroup>
```

### 2. Application Initialization
`Gir.Core` requires a different initialization sequence.
Replace `Gtk.Application.Init()` with:
```csharp
var application = Gtk.Application.New("io.github.subhra74.xdm", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) => {
    // Initialize UI here
};
application.Run(0, null);
```

### 3. Builder and Glade Files
`Gir.Core` supports `Gtk.Builder`, so your existing `.glade` files will work. However, object retrieval is slightly different:
```csharp
// Old GtkSharp:
var builder = new Gtk.Builder(null, "MainWindow.glade", null);
var window = (Gtk.Window)builder.GetObject("MainWindow");

// New Gir.Core:
var builder = Gtk.Builder.NewFromFile("glade/MainWindow.glade");
var window = (Gtk.Window)builder.GetObject("MainWindow");
```

### 4. Event Handlers
Events in `Gir.Core` use the `On[Event]` naming convention instead of `[Event]`.
```csharp
// Old GtkSharp:
button.Clicked += Button_Clicked;

// New Gir.Core:
button.OnClicked += Button_Clicked;
```

### 5. Memory Management
`Gir.Core` is stricter with GLib reference counting. You may need to manually manage the lifecycle of complex native objects, whereas `GtkSharp` aggressively attempted to garbage collect them (sometimes leading to crashes).

## Conclusion
Due to the sheer size of `XDM.Gtk.UI` (50,000+ lines of UI code across 40+ files), the migration should be done incrementally, starting with a minimal standalone test of the MainWindow.
