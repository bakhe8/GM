# Presentation Layer

This folder is the official WPF presentation surface for the current UI rebuild.

## Structure

- `Shell/`
  - Owns the application shell opened by `App.xaml.cs`.
  - Contains `MainWindow.xaml`, shell startup behavior, shell view model, shell commands, and shell display models.
- `Views/`
  - Contains workspace surfaces shown from sidebar navigation.
  - `Guarantees/GuaranteesDashboardView.xaml`: the approved guarantees workspace, including toolbar, KPI cards, guarantees table, paging, and guidance cards.
  - `Guarantees/GuaranteeDetailPanel.xaml`: the approved right-side guarantee detail panel, timeline, attachments, and quick actions.
  - Current workspaces are grouped by feature: `Dashboard`, `Requests`, `Banks`, `Reports`, `Settings`, and shared workspace chrome in `Common`.
- `Dialogs/`
  - Contains modal and side workflow dialogs used by the shell and workspace actions.
- `Themes/`
  - Contains shared ResourceDictionary files such as application scroll bar styling.
  - `Colors.xaml`: official shell brushes.
  - `Effects.xaml`: shared shadows and visual effects.
  - `Buttons.xaml`: chrome, primary, row, link, and navigation button styles.
  - `Inputs.xaml`: filter ComboBox and search TextBox styles.
  - `Cards.xaml`: card container style.
  - `Typography.xaml`: KPI text styles.
  - `Tables.xaml`: table header, cell, and status text styles.
    - Also owns the reusable reference table pattern: `ReferenceTableContainer`, `ReferenceTableHeaderBand`, `ReferenceTableRowsListBox`, `ReferenceTableRowItem`, and pager styles.
    - Any future workspace table should start from these resources so it visually matches the approved guarantees table.
  - `Navigation.xaml`: sidebar icon and label text styles.
  - `ScrollBars.xaml`: global slim scroll bar styling merged from `App.xaml`.
- `Converters/`
  - Contains WPF value converters used by XAML resources and bindings.

## Boundaries

- Legacy UI files remain archived under `archive/v1_views` and are excluded from the build by `GuaranteeManager.csproj`.
- Shared icon and image assets remain in their current project locations, including `IconsDictionary.xaml` and `Assets/Logos`, to preserve approved visual work and resource paths.
- Future screens should be added under `Presentation/Views`, while sidebar navigation should replace the workspace content without disturbing the approved shell, detail panel, sidebar, and status bar structure.
