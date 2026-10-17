using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using CloudBerry.App.Services;

namespace CloudBerry.App;

/// <summary>
/// Borderless modal shown before the splash/main window during the private testing phase.
/// Blocks startup until a valid invitation key is entered or the user chooses to exit.
/// The local grant is remembered; keys do not have globally enforced activation limits.
/// </summary>
public partial class AccessGateWindow : Window
{
    /// <summary>True once a valid key has been entered and accepted this session.</summary>
    public bool AccessGranted { get; private set; }

    public AccessGateWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => KeyInput.Focus();
    }

    private void RootBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Mouse was released mid-call (e.g. a fast click) - nothing to do.
            }
        }
    }

    private void KeyInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AttemptRedeem();
        }
    }

    private void KeyInputTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ErrorText.Visibility == Visibility.Visible)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void ContinueClick(object sender, RoutedEventArgs e)
    {
        AttemptRedeem();
    }

    private void AttemptRedeem()
    {
        if (AccessGateService.TryRedeem(KeyInput.Text))
        {
            AccessGranted = true;
            DialogResult = true;
            Close();
            return;
        }

        ErrorText.Visibility = Visibility.Visible;
        KeyInput.SelectAll();
        KeyInput.Focus();

        if (TryFindResource("KeyFieldShakeStoryboard") is Storyboard shake)
        {
            shake.Begin(KeyFieldBorder);
        }
    }

    private void ExitClick(object sender, RoutedEventArgs e)
    {
        AccessGranted = false;
        DialogResult = false;
        Close();
    }
}
