using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ProjectIndexer.Wpf.ViewModels;

namespace ProjectIndexer.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            try { _vm.LoadFromDatabaseCommand.Execute(null); }
            catch (Exception ex) { _vm.StatusText = $"Load error: {ex.Message}"; }
        };

    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
        {
            string property = header.Column.Header?.ToString() switch
            {
                "Name" => "Name",
                "Path" => "FullPath",
                "Size" => "Size",
                "Date Modified" => "DateModified",
                "Date Created" => "DateCreated",
                "Type" => "Type",
                "Attrs" => "Attributes",
                "Source" => "Source",
                _ => header.Column.DisplayMemberBinding is Binding b ? b.Path.Path : "Name",
            };

            var view = _vm.ResultsView;
            var sortDir = ListSortDirection.Ascending;

            if (view.SortDescriptions.Count > 0)
            {
                var existing = view.SortDescriptions[0];
                if (existing.PropertyName == property)
                {
                    sortDir = existing.Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }
                view.SortDescriptions.Clear();
            }

            view.SortDescriptions.Add(new SortDescription(property, sortDir));
        }
    }

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = (sender as System.Windows.Controls.ListView)?.SelectedItem as FileEntryViewModel;
        if (item == null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FullPath,
                UseShellExecute = true,
            });
        }
        catch { }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _vm.SearchText = "";
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
