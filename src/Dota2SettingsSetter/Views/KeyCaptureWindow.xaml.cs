using System.Windows;
using System.Windows.Input;
using Dota2SettingsSetter.Core;

namespace Dota2SettingsSetter.Views;

/// <summary>Waits for the next key, mouse button or wheel tick and returns it as a Dota chord.</summary>
public partial class KeyCaptureWindow : Window
{
    /// <summary>The chosen key; empty when the user picked Unbind.</summary>
    public KeyChord Result { get; private set; }

    // A modifier pressed on its own binds that modifier key, but only if it's released
    // without another key in between (otherwise it was the start of a combo).
    Key loneModifier = Key.None;

    public KeyCaptureWindow(string actionLabel, KeyChord current)
    {
        InitializeComponent();
        ActionText.Text = actionLabel;
        CurrentText.Text = "Currently " + current.Display;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = WpfKeys.RealKey(e);
        if (key == Key.Escape)
        {
            DialogResult = false;
            return;
        }
        if (WpfKeys.IsModifier(key))
        {
            if (!e.IsRepeat)
                loneModifier = key;
            ShowPreview();
            return;
        }

        loneModifier = Key.None;
        var name = WpfKeys.ToDota(key, e);
        if (name == null)
        {
            ErrorText.Text = "Dota can't bind that key.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Accept(new KeyChord(name, WpfKeys.CurrentModifier()));
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        var key = WpfKeys.RealKey(e);
        if (key != Key.None && key == loneModifier)
        {
            Accept(new KeyChord(WpfKeys.ToDota(key)));
            return;
        }
        ShowPreview();
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        var name = WpfKeys.ToDota(e.ChangedButton);
        if (name == null)
            return;
        e.Handled = true;
        Accept(new KeyChord(name, WpfKeys.CurrentModifier()));
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        Accept(new KeyChord(e.Delta > 0 ? "MWHEELUP" : "MWHEELDOWN", WpfKeys.CurrentModifier()));
    }

    void ShowPreview()
    {
        var modifier = WpfKeys.CurrentModifier();
        PreviewText.Text = modifier.Length == 0 ? "Press a key…" : KeyNames.DisplayModifier(modifier) + " + …";
    }

    void Accept(KeyChord chord)
    {
        Result = chord;
        DialogResult = true;
    }

    void Unbind_Click(object sender, RoutedEventArgs e) => Accept(KeyChord.None);

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
