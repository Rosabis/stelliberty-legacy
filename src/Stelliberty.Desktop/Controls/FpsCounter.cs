using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Stelliberty.Desktop.Controls;

public sealed class FpsCounter : Control
{
    internal static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromMilliseconds(16);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<FpsCounter, IBrush?>(nameof(Foreground));

    private readonly Stopwatch _stopwatch = new();
    private int _frames;
    private bool _running;
    private bool _frameRequestPending;
    private long _lastCompositionTimestamp;
    private string _text = "-- FPS";

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public FontFamily FontFamily { get; init; } = FontFamily.Default;

    public double FontSize { get; init; } = 12;

    public FontWeight FontWeight { get; init; } = FontWeight.Normal;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _running = true;
        _frames = 0;
        _frameRequestPending = false;
        _lastCompositionTimestamp = 0;
        _stopwatch.Restart();
        RequestNextFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _running = false;
        _frameRequestPending = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = BuildText();
        return new Size(text.Width, text.Height);
    }

    public override void Render(DrawingContext context)
    {
        var text = BuildText();
        var top = (Bounds.Height - text.Height) / 2;
        context.DrawText(text, new Point(0, top > 0 ? top : 0));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            if (change.NewValue is true && _running)
            {
                RequestNextFrame();
            }
            else if (change.NewValue is false)
            {
                _frameRequestPending = false;
            }
        }
    }

    // Some legacy Windows composition backends can complete updates without vsync throttling.
    // Keep the title bar counter sampled at about 60 Hz so it cannot become a UI-thread busy loop.
    private void RequestNextFrame()
    {
        if (!_running || !IsVisible || _frameRequestPending)
        {
            return;
        }

        var delay = GetNextSampleDelay(Stopwatch.GetTimestamp(), _lastCompositionTimestamp);
        if (delay > TimeSpan.Zero)
        {
            DispatcherTimer.RunOnce(RequestNextFrame, delay, DispatcherPriority.Background);
            return;
        }

        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor is null)
        {
            // 合成视觉还没就绪；下一帧重试。
            Dispatcher.UIThread.Post(RequestNextFrame, DispatcherPriority.Background);
            return;
        }

        _frameRequestPending = true;
        compositor.RequestCompositionUpdate(OnComposed);
    }

    private void OnComposed()
    {
        _frameRequestPending = false;
        if (!_running)
        {
            return;
        }

        _lastCompositionTimestamp = Stopwatch.GetTimestamp();
        _frames++;
        var elapsed = _stopwatch.ElapsedMilliseconds;
        if (elapsed >= 1000)
        {
            var next = $"{(int)(_frames * 1000.0 / elapsed)} FPS";
            _frames = 0;
            _stopwatch.Restart();
            if (next != _text)
            {
                _text = next;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        RequestNextFrame();
    }

    internal static TimeSpan GetNextSampleDelay(long nowTimestamp, long lastCompositionTimestamp)
    {
        if (lastCompositionTimestamp <= 0 || nowTimestamp <= lastCompositionTimestamp)
        {
            return TimeSpan.Zero;
        }

        var elapsed = Stopwatch.GetElapsedTime(lastCompositionTimestamp, nowTimestamp);
        return elapsed >= MinimumSampleInterval ? TimeSpan.Zero : MinimumSampleInterval - elapsed;
    }

    private FormattedText BuildText() => new(
        _text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle.Normal, FontWeight),
        FontSize,
        Foreground ?? Brushes.Gray);
}
