using System.Windows;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

/// <summary>
/// Review window for the Duplicate Ownership Detector. Lists every unresolved title cluster
/// CloudBerry found across sources and lets the player link or dismiss each one individually —
/// every action commits immediately (no batch Confirm step), since linking/dismissing is cheap,
/// reversible-in-spirit (dismissals and links can both be undone from the game detail panel or
/// by re-running detection later), and reviewing several groups back-to-back should feel fluid.
/// </summary>
public partial class DuplicateOwnershipWindow : Window
{
    private readonly DuplicateOwnershipViewModel _viewModel;

    public DuplicateOwnershipWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _viewModel = new DuplicateOwnershipViewModel(mainViewModel, mainViewModel.BuildPotentialDuplicateGroups());
        DataContext = _viewModel;
    }

    private async void LinkGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DuplicateGroupEntryViewModel group })
        {
            return;
        }

        ((FrameworkElement)sender).IsEnabled = false;
        await group.LinkAsSameGameAsync();
    }

    private async void DismissGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DuplicateGroupEntryViewModel group })
        {
            return;
        }

        ((FrameworkElement)sender).IsEnabled = false;
        await group.DismissAsync();
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
