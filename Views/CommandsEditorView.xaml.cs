using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Toolkit.ViewModels;

namespace Toolkit.Views;

public partial class CommandsEditorView : UserControl
{
    public CommandsEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is CommandsEditorViewModel vm)
                vm.IconSelected += () => IconPickerPopup.IsOpen = false;
        };
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is CommandsEditorViewModel vm)
            vm.SelectTreeItem(e.NewValue);
    }

    private void OnTreePreviewRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var pos = e.GetPosition(MainTree);
        var hit = VisualTreeHelper.HitTest(MainTree, pos)?.VisualHit;
        while (hit != null && hit is not TreeViewItem)
            hit = VisualTreeHelper.GetParent(hit);

        var vm = DataContext as CommandsEditorViewModel;
        if (vm == null || !vm.IsEditorEnabled) return;

        var cm = new ContextMenu();
        cm.DataContext = DataContext;

        if (hit is TreeViewItem tvi && tvi.IsVisible)
        {
            tvi.IsSelected = true;
            vm.SelectTreeItem(tvi.DataContext);

            var addItem = new MenuItem { Header = "Agregar comando" };
            addItem.SetBinding(MenuItem.CommandProperty, new Binding("AddItemCommand"));
            cm.Items.Add(addItem);

            var delete = new MenuItem { Header = "Eliminar", Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)) };
            delete.SetBinding(MenuItem.CommandProperty, new Binding("DeleteSelectedCommand"));
            cm.Items.Add(delete);
        }
        else
        {
            vm.SelectedCategoria = null;
            vm.SelectedCommand = null;

            var addCat = new MenuItem { Header = "Agregar categoría", Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)) };
            addCat.SetBinding(MenuItem.CommandProperty, new Binding("AddCategoriaCommand"));
            cm.Items.Add(addCat);
        }

        cm.PlacementTarget = MainTree;
        cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        cm.Closed += (_, _) => cm.DataContext = null;
        cm.IsOpen = true;
    }

    private void ToggleIconPicker(object sender, RoutedEventArgs e)
    {
        IconPickerPopup.IsOpen = !IconPickerPopup.IsOpen;
    }
}
