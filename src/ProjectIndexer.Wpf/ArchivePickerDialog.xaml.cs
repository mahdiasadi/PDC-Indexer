using System.Windows;
using System.Windows.Data;
using ProjectIndexer.Core.Archiving;

namespace ProjectIndexer.Wpf;

public partial class ArchivePickerDialog : Window
{
    public ArchiveInfo? SelectedArchive { get; private set; }

    public ArchivePickerDialog(List<ArchiveInfo> archives)
    {
        InitializeComponent();

        var grouped = archives
            .GroupBy(a => $"{a.DriveLetter}:\\")
            .OrderBy(g => g.Key)
            .ToList();

        var view = CollectionViewSource.GetDefaultView(grouped.SelectMany(g => g));
        view.GroupDescriptions.Add(new PropertyGroupDescription("DriveLetter"));
        ArchiveList.ItemsSource = view;
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        SelectedArchive = ArchiveList.SelectedItem as ArchiveInfo;
        if (SelectedArchive != null)
        {
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
