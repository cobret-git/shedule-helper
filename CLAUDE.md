# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Schedule Helper is a .NET 8 WPF application with modern UI features including custom window chrome, theme switching, and Windows 11 visual enhancements.

## Technology Stack

- **.NET 8.0** (Windows target framework: `net8.0-windows`)
- **WPF** for UI framework
- **CommunityToolkit.Mvvm** (v8.4.0) for MVVM pattern implementation
- **Entity Framework Core with SQLite** (v9.0.11) for data persistence
- **Serilog.Sinks.File** (v7.0.0) for logging

## Build and Run Commands

All commands should be run from the `src` directory:

```bash
# Build the solution
dotnet build SheduleHelper.sln

# Run the application
dotnet run --project SheduleHelper.WpfApp/SheduleHelper.WpfApp.csproj

# Build for release
dotnet build SheduleHelper.sln -c Release

# Clean build artifacts
dotnet clean SheduleHelper.sln
```

## Project Structure

```
src/
└── SheduleHelper.WpfApp/          # Main WPF application project
    ├── Assets/
    │   ├── Logo/                   # Application icons (.ico, .svg)
    │   └── Resources/              # XAML resource dictionaries (themes, palettes, styles)
    ├── ViewModel/                  # ViewModels for MVVM pattern
    ├── App.xaml[.cs]              # Application entry point
    └── MainWindow.xaml[.cs]       # Main window with custom chrome
```

## Architecture

### MVVM Pattern

The application follows the MVVM (Model-View-ViewModel) pattern:
- **Views**: XAML files (MainWindow.xaml, etc.)
- **ViewModels**: Located in `ViewModel/` folder, using CommunityToolkit.Mvvm attributes like `[RelayCommand]`
- **Models**: (Future) Data models for Entity Framework Core

### Theme System

The application supports dynamic theme switching between Light and Dark themes:

- **Theme Resources**: Located in `Assets/Resources/`
  - `LightTheme.xaml` / `DarkTheme.xaml` - Complete theme definitions
  - `LightPalette.xaml` / `DarkPalette.xaml` - Color palettes
  - `DefaultControlStyles.xaml` - Control style definitions

- **Theme Switching**: Implemented in `MainWindow.xaml.cs:95` (`SwitchThemeButton_Click`)
  - Creates a bitmap snapshot of the current UI
  - Switches theme resources instantly
  - Fades out the snapshot with a 300ms animation for smooth transition
  - Default theme is Light (set in `App.xaml:9`)

### UI Design Guidelines

The application follows Material Design principles for WPF control styling. Detailed guidelines are available in:
- **Design Document**: `docs/Styling Standard WPF Controls with Material Design Principles.pdf`

**Key Design Principles:**

1. **Palette-Based Theming**: Controls should reference colors from theme palettes (`LightPalette.xaml` / `DarkPalette.xaml`) rather than hard-coded colors

2. **Disabled State Styling** (**IMPORTANT OVERRIDE**):

   **General Principle:**
   - **DO NOT** use solid brushes to change content color in disabled states (e.g., `ForegroundDisabledBrush`)
   - **DO** use the `Opacity` property for disabled states
   - **Reason**: Preserves multi-colored content (images, colored text) rather than flattening to a single color

   **Implementation Steps:**

   **Step 1: Define Opacity Resources in Palettes**

   In both `LightPalette.xaml` and `DarkPalette.xaml`, define opacity values as `system:Double` resources:

   ```xaml
   <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       xmlns:system="clr-namespace:System;assembly=mscorlib">

       <!-- For Default Controls (buttons, etc.) -->
       <system:Double x:Key="DefaultControlDisabledOpacity" x:Shared="False">0.8</system:Double>

       <!-- For Primary Controls -->
       <system:Double x:Key="PrimaryControlDisabledOpacity" x:Shared="False">0.8</system:Double>

       <!-- For Input Controls (textboxes, etc.) -->
       <system:Double x:Key="InputControlDisabledOpacity" x:Shared="False">0.8</system:Double>
   </ResourceDictionary>
   ```

   **Step 2: Apply Opacity to Content Elements in Control Templates**

   **CRITICAL**: Apply opacity to the **content element** (e.g., `ContentPresenter`), NOT to the entire control or root element.

   ```xaml
   <ControlTemplate TargetType="{x:Type Button}">
       <Grid>
           <Border x:Name="MainBorder" ...>
               <ContentPresenter x:Name="ContentPresenter" .../>
           </Border>
       </Grid>

       <ControlTemplate.Triggers>
           <Trigger Property="IsEnabled" Value="False">
               <!-- Set background and border for disabled appearance -->
               <Setter TargetName="MainBorder" Property="Background"
                       Value="{DynamicResource DefaultControlBackgroundDisabledBrush}"/>
               <Setter TargetName="MainBorder" Property="BorderBrush"
                       Value="{DynamicResource DefaultControlBorderDisabledBrush}"/>

               <!-- Apply opacity ONLY to ContentPresenter -->
               <Setter TargetName="ContentPresenter" Property="Opacity"
                       Value="{DynamicResource DefaultControlDisabledOpacity}"/>

               <Setter Property="Cursor" Value="Arrow"/>
           </Trigger>
       </ControlTemplate.Triggers>
   </ControlTemplate>
   ```

   **Why Target ContentPresenter?**
   - Applying opacity to the entire control would dim the background and border too
   - ContentPresenter contains the actual content (text, images, icons)
   - This approach keeps the disabled background/border colors intact while dimming only the content

   **Example from DefaultButtonStyle** (`DefaultControlStyles.xaml:105`):
   ```xaml
   <Setter TargetName="ContentPresenter" Property="Opacity"
           Value="{DynamicResource DefaultControlDisabledOpacity}"/>
   ```

3. **Control States**: Define clear visual states for:
   - Normal
   - MouseOver / Hover
   - Pressed
   - Disabled (using opacity)
   - Focused

4. **Consistency**: All controls should follow the same color palette and state transition patterns defined in the design document

**Note**: The design document is kept in a shared location (not committed to git) and copied to `docs/` for local use. If the file is missing, copy it from `C:\Users\konst\Documents\Claude Shared\`.

### Custom Window Chrome

The MainWindow implements custom window chrome using Win32 interop:

- **Window Style Management**: Uses User32.dll P/Invoke for native window behavior
  - `WS_CAPTION`, `WS_SIZEBOX`, `WS_MINIMIZEBOX`, `WS_MAXIMIZEBOX`, `WS_SYSMENU` flags
  - Native minimize/maximize/restore animations via `ShowWindow()`

- **Desktop Window Manager (DWM) Integration**:
  - Enables DWM composition for modern visual effects
  - Configures rounded corners for Windows 11 (`DWMWCP_ROUND`)
  - Manages window transitions (`DWMWA_TRANSITIONS_FORCEDISABLED`)

- **Custom Title Bar**:
  - Drag-to-move functionality
  - Double-click to maximize/restore
  - Custom minimize, maximize/restore, and close buttons

- **Monitor-Aware Maximization**:
  - Handles `WM_GETMINMAXINFO` message to properly maximize on multi-monitor setups
  - Respects working area (taskbar) boundaries

## Code Organization

Classes in this codebase follow a consistent region-based organization pattern:

1. **Fields** - Constants, public fields, private readonly fields, private fields
2. **Constructors** - All constructor overloads
3. **Events** - Event declarations
4. **Properties** - Public properties first, then private properties
5. **Methods** - Public methods, then `[RelayCommand]` methods, then private methods, then partial methods
6. **Handlers** - Event handlers (e.g., `OnSelectedItemChanged`, `OnOpenWindowRequestReceived`)
7. **Helpers** - Private helper methods for code encapsulation
8. **CanExecute** - (Optional) Boolean methods for RelayCommand execution logic
9. **Native Methods** - P/Invoke declarations (grouped by DLL: User32, DWM, etc.)
10. **Structures** - Native structures for P/Invoke

Additional regions may be added as needed for specific class requirements.

## Key Implementation Details

### Render Mode Configuration

The application sets `RenderOptions.ProcessRenderMode = Default` in `App.xaml.cs:15` to ensure proper rendering behavior.

### Window Interop Lifecycle

The MainWindow uses a two-stage initialization:
1. **SourceInitialized** event: Attaches the WndProc hook for message processing
2. **Loaded** event: Configures DWM attributes and window styles

This separation ensures proper initialization order for Win32 interop functionality.

### Entity Framework Core

The project references Entity Framework Core with SQLite provider, though database context and models may not yet be implemented. When adding:
- Place DbContext in a `Data/` folder
- Place entity models in a `Models/` folder
- Configure connection string for SQLite database

### Logging

Serilog is configured for file-based logging. Log files will be written to the application directory.

## Version Information

Current version: `1.0.15.2511` (set in .csproj AssemblyVersion and FileVersion)

## License

This project is licensed under the Apache License 2.0. See LICENSE file for details.
