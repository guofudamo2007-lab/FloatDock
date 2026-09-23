using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using FloatDock.Core;

namespace FloatDock.Windows;

internal sealed class DockIcon : Button
{
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _lift = new();
    private readonly TranslateTransform _bounce = new();
    private readonly TranslateTransform _neighbor = new();
    private double _shift;
    private readonly Border _indicator;
    private readonly Image _image;
    private bool _active;
    private MotionTarget _target = new(1, 0);
    public DockEntry Entry { get; private set; }

    public DockIcon(DockEntry entry, int size)
    {
        Entry = entry;
        Width = size + 16; Height = 132;
        Cursor = Cursors.Hand; Background = Brushes.Transparent; BorderThickness = new(0);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        Template = new ControlTemplate(typeof(Button)) { VisualTree = presenter };
        var image = _image = new Image { Source = WindowService.Icon(entry), Width = size, Height = size,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = new(.5, .8), IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        var transforms = new TransformGroup(); transforms.Children.Add(_scale); transforms.Children.Add(_lift); transforms.Children.Add(_bounce);
        image.RenderTransform = transforms;
        image.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = .22 };
        _indicator = new Border { Width = 4, Height = 4, CornerRadius = new(2), Background = Brushes.White,
            VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, 5) };
        var grid = new Grid { Background = Brushes.Transparent, RenderTransform = _neighbor };
        grid.Children.Add(image); grid.Children.Add(_indicator); Content = grid;
        ToolTipService.SetInitialShowDelay(this, 550); ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Top);
        Update(entry);
    }

    public void Update(DockEntry entry)
    {
        Entry = entry;
        ToolTip = entry.Windows.Count > 0 ? string.Join("\n", entry.Windows.Select(w => w.Title)) : entry.Name;
        AutomationProperties.SetName(this, entry.Name);
        _indicator.Visibility = entry.Windows.Count > 0 ? Visibility.Visible : Visibility.Hidden;
    }

    internal Rect ImageBounds(Visual ancestor) => _image.TransformToAncestor(ancestor).TransformBounds(new Rect(_image.RenderSize));

    public void SetState(double distance, bool active, bool reduced, double shift = 0)
    {
        shift = reduced ? 0 : shift;
        if (_shift != shift) { _shift = shift; Animate(_neighbor, TranslateTransform.XProperty, shift, reduced); }
        if (_active != active)
        {
            _active = active;
            _indicator.Width = active ? 16 : 4;
            _indicator.Background = active ? new SolidColorBrush(Color.FromRgb(143, 195, 255)) : Brushes.White;
        }
        var target = DockModel.Motion(distance, active, reduced);
        if (reduced) _bounce.BeginAnimation(TranslateTransform.YProperty, null);
        if (_target == target) return;
        _target = target;
        Animate(_scale, ScaleTransform.ScaleXProperty, target.Scale, reduced);
        Animate(_scale, ScaleTransform.ScaleYProperty, target.Scale, reduced);
        Animate(_lift, TranslateTransform.YProperty, -target.Lift, reduced);
    }

    public void Bounce(bool reduced)
    {
        if (reduced) return;
        var frames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(390), FillBehavior = FillBehavior.Stop };
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(-13, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)), new CubicEase { EasingMode = EasingMode.EaseOut }));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330))));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(390))));
        _bounce.BeginAnimation(TranslateTransform.YProperty, frames);
    }

    private static void Animate(Animatable target, DependencyProperty property, double value, bool instant)
    {
        if (instant) { target.BeginAnimation(property, null); target.SetValue(property, value); return; }
        target.BeginAnimation(property, new DoubleAnimation(value, TimeSpan.FromMilliseconds(180))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
