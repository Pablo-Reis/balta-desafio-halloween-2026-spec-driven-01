using System.Windows;
using Microsoft.Win32;
using Productivity.Domain;

namespace Productivity.App.Support;

/// <summary>Alterna a paleta Aurora (escura/clara) acompanhando a configuração de tema do app.</summary>
public static class AuroraTheme
{
    private const string DarkPalette = "Styles/Aurora.Palette.Dark.xaml";
    private const string LightPalette = "Styles/Aurora.Palette.Light.xaml";

    private static AppTheme _mode = AppTheme.System;
    private static bool _subscribed;

    public static void Apply(AppTheme mode)
    {
        _mode = mode;
        if (!_subscribed)
        {
            _subscribed = true;
            SystemEvents.UserPreferenceChanged += (_, _) =>
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (_mode == AppTheme.System)
                        Swap(IsDark(_mode));
                });
        }

        Swap(IsDark(mode));
    }

    private static bool IsDark(AppTheme mode) => mode switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => SystemUsesDarkTheme()
    };

    private static bool SystemUsesDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int light || light == 0;
    }

    private static void Swap(bool dark)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var index = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Aurora.Palette", StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        var palette = new ResourceDictionary { Source = new Uri(dark ? DarkPalette : LightPalette, UriKind.Relative) };
        if (index >= 0)
            dictionaries[index] = palette;
        else
            dictionaries.Insert(0, palette);
    }
}
