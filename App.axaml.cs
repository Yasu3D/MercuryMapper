using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MercuryMapper;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SetCulture("en-US");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Application.Current.Resources["PlatformMargin"] = new Thickness(70, 0, 0, 0); // macOS margin
        }
    }

    public static void SetCulture(string culture)
    {
        Assets.Lang.Resources.Culture = new(culture);
    }
}