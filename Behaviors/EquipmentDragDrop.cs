using System.Windows;
using System.Windows.Input;
using MilitarySupply.Models;
using MilitarySupply.ViewModels;
namespace MilitarySupply.Behaviors;

// View-only input adapter: the ViewModel decides compatibility and stages the item.
public static class EquipmentDragDrop
{
    const string Format = "MilitarySupply.StockItem";
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached("Source", typeof(bool), typeof(EquipmentDragDrop), new PropertyMetadata(false, SourceChanged));
    public static bool GetSource(DependencyObject d) => (bool)d.GetValue(SourceProperty);
    public static void SetSource(DependencyObject d, bool value) => d.SetValue(SourceProperty, value);
    public static readonly DependencyProperty SlotProperty = DependencyProperty.RegisterAttached("Slot", typeof(string), typeof(EquipmentDragDrop), new PropertyMetadata(null, SlotChanged));
    public static string? GetSlot(DependencyObject d) => (string?)d.GetValue(SlotProperty);
    public static void SetSlot(DependencyObject d, string value) => d.SetValue(SlotProperty, value);
    public static readonly DependencyProperty HoverProperty = DependencyProperty.RegisterAttached("Hover", typeof(bool), typeof(EquipmentDragDrop), new PropertyMetadata(false));
    public static bool GetHover(DependencyObject d) => (bool)d.GetValue(HoverProperty);
    public static void SetHover(DependencyObject d, bool value) => d.SetValue(HoverProperty, value);
    static readonly DependencyProperty StartProperty = DependencyProperty.RegisterAttached("Start", typeof(Point), typeof(EquipmentDragDrop));
    static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement f) return;
        f.PreviewMouseLeftButtonDown -= Down; f.PreviewMouseMove -= Move; f.PreviewMouseLeftButtonUp -= Up;
        if ((bool)e.NewValue) { f.PreviewMouseLeftButtonDown += Down; f.PreviewMouseMove += Move; f.PreviewMouseLeftButtonUp += Up; }
    }
    static void Down(object sender, MouseButtonEventArgs e)
    {
        var f = (FrameworkElement)sender;
        for (var node = e.OriginalSource as DependencyObject; node != null && node != f; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;
        f.SetValue(StartProperty, e.GetPosition(f)); f.CaptureMouse();
    }
    static void Up(object sender, MouseButtonEventArgs e) { var f = (FrameworkElement)sender; if (f.IsMouseCaptured) f.ReleaseMouseCapture(); }
    static void Move(object sender, MouseEventArgs e)
    {
        var f = (FrameworkElement)sender;
        if (e.LeftButton != MouseButtonState.Pressed || !f.IsMouseCaptured || f.DataContext is not StockItem item) return;
        var start = (Point)f.GetValue(StartProperty); var p = e.GetPosition(f);
        if (Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        f.ReleaseMouseCapture(); DragDrop.DoDragDrop(f, new DataObject(Format, item), DragDropEffects.Copy); e.Handled = true;
    }
    static void SlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement f) return;
        f.AllowDrop = e.NewValue != null;
        f.DragOver -= Over; f.DragLeave -= Leave; f.Drop -= Drop;
        if (f.AllowDrop) { f.DragOver += Over; f.DragLeave += Leave; f.Drop += Drop; }
    }
    static MainViewModel? Vm(FrameworkElement f) => Window.GetWindow(f)?.DataContext as MainViewModel;
    static void Over(object sender, DragEventArgs e)
    {
        var f = (FrameworkElement)sender; var item = e.Data.GetDataPresent(Format) ? e.Data.GetData(Format) as StockItem : null;
        var valid = item != null && Vm(f)?.CanStageItem(item, GetSlot(f)) == true;
        SetHover(f, valid); e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
    }
    static void Leave(object sender, DragEventArgs e) => SetHover((FrameworkElement)sender, false);
    static void Drop(object sender, DragEventArgs e)
    {
        var f = (FrameworkElement)sender; SetHover(f, false);
        if (e.Data.GetDataPresent(Format) && e.Data.GetData(Format) is StockItem item) Vm(f)?.StageItem(item, GetSlot(f));
        e.Handled = true;
    }
}
