using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dota2SettingsSetter.Views;

/// <summary>A themed message box with any set of buttons.</summary>
public partial class ChoiceWindow : Window
{
    int choice = -1;

    ChoiceWindow(string title, string message, string[] buttons, int accentIndex)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Content = buttons[i],
                Margin = new Thickness(8, 0, 0, 0),
                MinWidth = 84,
            };
            if (i == accentIndex)
            {
                button.Style = (Style)FindResource("AccentButton");
                button.IsDefault = true;
            }
            button.Click += (s, e) =>
            {
                choice = index;
                DialogResult = true;
            };
            ButtonPanel.Children.Add(button);
        }
        MouseLeftButtonDown += (s, e) => DragMove();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
        }
        base.OnPreviewKeyDown(e);
    }

    /// <summary>Shows the dialog and returns the clicked button's index, or -1 when dismissed.</summary>
    public static int Show(Window owner, string title, string message, int accentIndex, params string[] buttons)
    {
        var window = new ChoiceWindow(title, message, buttons, accentIndex) { Owner = owner };
        window.ShowDialog();
        return window.choice;
    }

    public static void Info(Window owner, string title, string message) => Show(owner, title, message, 0, "OK");
}
