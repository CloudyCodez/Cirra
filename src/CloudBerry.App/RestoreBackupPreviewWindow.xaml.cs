using System.Windows;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App;

/// <summary>
/// Shows what a backup/export file contains before the player commits to replacing their live
/// library with it. The actual restore (and the restart prompt that follows) happens in
/// MainWindow.xaml.cs after this window returns true, mirroring how CleanMissingPreviewWindow
/// only *previews* — it never touches the library itself.
/// </summary>
public partial class RestoreBackupPreviewWindow : Window
{
    public RestoreBackupPreviewWindow(MainViewModel mainViewModel, string sourceFilePath, LibrarySnapshot snapshot)
    {
        InitializeComponent();
        DataContext = new RestoreBackupPreviewViewModel(mainViewModel, sourceFilePath, snapshot);
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RestoreClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "This will replace your current library. Cirra will back up what you have now first, then restart. Continue?",
            "Confirm restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
