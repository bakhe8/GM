using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace GuaranteeManager.Utils
{
    public static class DataGridContextMenuAssist
    {
        public static bool TrySelectRowFromRightClick(DataGrid dataGrid, MouseButtonEventArgs e)
        {
            if (dataGrid == null)
            {
                return false;
            }

            DependencyObject? source = e.OriginalSource as DependencyObject;
            if (FindAncestor<DataGridColumnHeader>(source) != null || FindAncestor<ScrollBar>(source) != null)
            {
                return false;
            }

            DataGridRow? row = FindAncestor<DataGridRow>(source);
            if (row == null)
            {
                return false;
            }

            if (!row.IsSelected)
            {
                dataGrid.SelectedItem = row.Item;
            }

            row.IsSelected = true;
            row.Focus();
            dataGrid.Focus();
            return true;
        }

        public static bool TryGetRowItem<T>(MouseButtonEventArgs e, out T? item) where T : class
        {
            item = null;

            DependencyObject? source = e.OriginalSource as DependencyObject;
            DataGridRow? row = FindAncestor<DataGridRow>(source);
            if (row?.Item is not T typedItem)
            {
                return false;
            }

            item = typedItem;
            return true;
        }

        public static bool IsPointerOverRow(DataGrid dataGrid)
        {
            if (dataGrid == null || !dataGrid.IsLoaded || dataGrid.ActualWidth <= 0 || dataGrid.ActualHeight <= 0)
            {
                return false;
            }

            Point position = Mouse.GetPosition(dataGrid);
            if (position.X < 0 || position.Y < 0 || position.X > dataGrid.ActualWidth || position.Y > dataGrid.ActualHeight)
            {
                return false;
            }

            DependencyObject? hit = dataGrid.InputHitTest(position) as DependencyObject;
            return FindAncestor<DataGridRow>(hit) != null;
        }

        public static void OpenContextMenuAtPointer(DataGrid dataGrid, ContextMenu? contextMenu)
        {
            if (dataGrid == null || contextMenu == null)
            {
                return;
            }

            contextMenu.PlacementTarget = dataGrid;
            contextMenu.Placement = PlacementMode.MousePoint;
            dataGrid.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
