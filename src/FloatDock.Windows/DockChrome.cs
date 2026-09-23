using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FloatDock.Windows;

internal static class DockChrome
{
    internal static Button Button(string text, string label, Action action, double width = 34)
    {
        var button = new Button { Content = text, ToolTip = label, Width = width, Height = 36, Padding = new(2), Margin = new(1, 0, 1, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(220, 227, 237)), Background = Brushes.Transparent, BorderThickness = new(0),
            FontFamily = new("Segoe UI"), FontSize = 14, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        var surface = new FrameworkElementFactory(typeof(Border));
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        surface.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new(RelativeSourceMode.TemplatedParent) });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        surface.AppendChild(content); var template = new ControlTemplate(typeof(Button)) { VisualTree = surface };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)))); template.Triggers.Add(hover); button.Template = template;
        AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
}
