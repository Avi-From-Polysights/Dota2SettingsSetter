using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Dota2SettingsSetter.Core;
using Dota2SettingsSetter.Views;
using AppWindow = Dota2SettingsSetter.MainWindow;

namespace Dota2SettingsSetter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    // Keep the app alive on unexpected errors and leave a log people can attach to an issue.
    void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        string logPath = null;
        try
        {
            Directory.CreateDirectory(AccountStore.DefaultRoot);
            logPath = Path.Combine(AccountStore.DefaultRoot, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:u}] v{AppWindow.AppVersion}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging is best effort.
        }

        var details = e.Exception.Message + (logPath == null ? "" : $"\n\nDetails were saved to {logPath}. Attaching it to a bug report helps a lot.");
        bool report;
        try
        {
            report = ChoiceWindow.Show(MainWindow, "Something went wrong", details, 1, "Close", "Report a bug") == 1;
        }
        catch (Exception)
        {
            // If the themed dialog is what broke, fall back to the plain one.
            report = MessageBox.Show(details + "\n\nOpen the bug tracker?", "Something went wrong", MessageBoxButton.YesNo, MessageBoxImage.Error)
                == MessageBoxResult.Yes;
        }
        if (report)
            AppWindow.OpenUrl(AppWindow.RepoUrl + "/issues/new/choose");
    }
}
