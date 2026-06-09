using System;
using Microsoft.UI.Xaml;
using Huddle.Views;

namespace Huddle;

public partial class App : Application
{
    private PeekPanelWindow? _panel;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            LogStartupError(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogStartupError(e.ExceptionObject as Exception);
        };
    }

    private static void LogStartupError(Exception? ex)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Huddle", "startup-error.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.AppendAllText(path,
                $"[{DateTime.Now:o}] {ex?.GetType().FullName}: {ex?.Message}\n{ex?.StackTrace}\n\n");
        }
        catch { /* nothing we can do */ }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _panel = new PeekPanelWindow();
            _panel.Closed += (_, _) => Exit();
            _panel.ShowPanel();
        }
        catch (Exception ex)
        {
            LogStartupError(ex);
            throw;
        }
    }
}
