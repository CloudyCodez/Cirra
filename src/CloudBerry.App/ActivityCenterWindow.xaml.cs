using System.Windows;
using System.Windows.Controls;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class ActivityCenterWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public ActivityCenterWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void CancelOperationClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid operationId })
        {
            ViewModel.CancelActivityOperation(operationId);
        }
    }
}
