using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Stelliberty.Application.Settings;
using Stelliberty.Presentation.ViewModels;

namespace Stelliberty.Desktop.Services;

internal sealed class WindowAppearanceService : IDisposable
{
    private MainWindow? _window;
    private SettingsThemeViewModel? _theme;

    public void Attach(MainWindow window, SettingsThemeViewModel theme)
    {
        if (_theme is not null)
        {
            _theme.ThemeChanged -= OnThemeChanged;
            _theme.WindowEffectChanged -= OnWindowEffectChanged;
        }

        if (_window is not null)
        {
            _window.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        }

        _window = window;
        _theme = theme;
        _theme.ThemeChanged += OnThemeChanged;
        _theme.WindowEffectChanged += OnWindowEffectChanged;
        _window.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ApplyTheme(theme.SelectedOption.Value);
        ApplyWindowEffect(theme.SelectedWindowEffect);
    }

    public void Reapply()
    {
        if (_theme is null)
        {
            return;
        }

        ApplyTheme(_theme.SelectedOption.Value);
        ApplyWindowEffect(_theme.SelectedWindowEffect);
    }

    public void Dispose()
    {
        if (_theme is not null)
        {
            _theme.ThemeChanged -= OnThemeChanged;
            _theme.WindowEffectChanged -= OnWindowEffectChanged;
        }

        if (_window is not null)
        {
            _window.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        }

        _theme = null;
        _window = null;
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        ApplyTheme(theme);
    }

    private void OnWindowEffectChanged(object? sender, WindowEffect effect)
    {
        ApplyWindowEffect(effect);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs args)
    {

        if (_theme is null || _theme.SelectedOption.Value != AppTheme.System)
        {
            return;
        }

        UpdateRootSurfaceForCurrentEffect(IsCurrentLightTheme());
    }

    private void ApplyTheme(AppTheme theme)
    {
        if (Avalonia.Application.Current is null)
        {
            return;
        }

        Avalonia.Application.Current.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => Avalonia.Styling.ThemeVariant.Light,
            AppTheme.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };

        UpdateRootSurfaceForCurrentEffect(IsLightTheme(theme));
    }

    private void ApplyWindowEffect(WindowEffect effect)
    {
        if (_window is null)
        {
            return;
        }

        // Windows 能力层已关闭 Mica/Acrylic（Win10 LTSB 不支持且吃显存）；仅 macOS Blur 会走到半透明分支。
        _window.TransparencyLevelHint = effect switch
        {
            WindowEffect.Blur => [WindowTransparencyLevel.AcrylicBlur],
            _ => [WindowTransparencyLevel.None]
        };

        UpdateRootSurfaceForCurrentEffect(IsCurrentLightTheme());
    }

    private bool IsCurrentLightTheme()
    {
        if (_window is null || _theme is null)
        {
            return false;
        }

        return IsLightTheme(_theme.SelectedOption.Value);
    }

    private bool IsLightTheme(AppTheme theme)
    {
        return theme == AppTheme.Light
            || theme == AppTheme.System && _window?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;
    }

    private void UpdateRootSurfaceForCurrentEffect(bool isLightTheme)
    {
        if (_window is null || _theme is null)
        {
            return;
        }

        var effect = _theme.SelectedWindowEffect;
        var surfaceBrush = new SolidColorBrush(isLightTheme ? ThemeSurfaceColors.Light : ThemeSurfaceColors.Dark);
        // Blur（macOS）用半透明根表面；其余一律实色，避免 Windows 透明合成开销。
        IBrush rootSurfaceBrush = effect == WindowEffect.Blur
            ? new SolidColorBrush(Color.Parse(isLightTheme ? "#B3FFFFFF" : "#B3212121"))
            : surfaceBrush;
        // 这里只处理窗口效果背景；其余样式归 Theme.axaml。
        _window.Resources["AppRootSurfaceBrush"] = rootSurfaceBrush;
        _window.Resources["AppDialogSurfaceBrush"] = surfaceBrush;
        _window.Resources["AppPopupSurfaceBrush"] = surfaceBrush;
        _window.Resources["ComboBoxPopupBackground"] = surfaceBrush;
        var isLightSurface = effect == WindowEffect.None
            ? _window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light
            : isLightTheme;
        _window.Resources["AppSidebarShadow"] = BoxShadows.Parse(isLightSurface
            ? "4 0 10 -4 #1A000000"
            : "4 0 10 -4 #28FFFFFF");
    }
}
