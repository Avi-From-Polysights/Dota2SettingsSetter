using System;
using System.Windows;
using System.Windows.Controls;

namespace Dota2SettingsSetter.Views;

/// <summary>Asks for a single line of text, with live validation.</summary>
public partial class PromptWindow : Window
{
    readonly Func<string, string> validate;

    PromptWindow(string title, string message, string initial, Func<string, string> validate)
    {
        InitializeComponent();
        this.validate = validate;
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        Input.Text = initial ?? "";
        Loaded += (s, e) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
        MouseLeftButtonDown += (s, e) => DragMove();
        Validate();
    }

    /// <summary>Returns the trimmed text, or null when cancelled.</summary>
    public static string Ask(Window owner, string title, string message, string initial, Func<string, string> validate)
    {
        var window = new PromptWindow(title, message, initial, validate) { Owner = owner };
        return window.ShowDialog() == true ? window.Input.Text.Trim() : null;
    }

    bool Validate()
    {
        var error = validate?.Invoke(Input.Text.Trim());
        ErrorText.Text = error ?? "";
        OkButton.IsEnabled = error == null;
        return error == null;
    }

    void Input_TextChanged(object sender, TextChangedEventArgs e) => Validate();

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Validate())
            DialogResult = true;
    }
}
