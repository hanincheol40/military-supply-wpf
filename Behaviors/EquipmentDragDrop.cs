using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MilitarySupply.Models;
using MilitarySupply.ViewModels;

namespace MilitarySupply.Behaviors;

// 화면에서 마우스로 끌어 놓는 입력만 처리한다.
// 어느 칸에 놓을 수 있는지, 실제로 담는 일은 MainViewModel이 정한다.
public static class EquipmentDragDrop
{
    // 끌고 다니는 데이터에 붙이는 이름
    private const string Format = "MilitarySupply.StockItem";

    // Source = True 인 곳에서 끌기를 시작할 수 있다 (오른쪽 군수품 카드)
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached("Source", typeof(bool), typeof(EquipmentDragDrop),
            new PropertyMetadata(false, SourceChanged));

    public static bool GetSource(DependencyObject d) => (bool)d.GetValue(SourceProperty);
    public static void SetSource(DependencyObject d, bool value) => d.SetValue(SourceProperty, value);

    // Slot = 장착 위치 이름 (Head, Armor ...). 값이 있으면 그곳에 놓을 수 있다
    public static readonly DependencyProperty SlotProperty =
        DependencyProperty.RegisterAttached("Slot", typeof(string), typeof(EquipmentDragDrop),
            new PropertyMetadata(null, SlotChanged));

    public static string? GetSlot(DependencyObject d) => (string?)d.GetValue(SlotProperty);
    public static void SetSlot(DependencyObject d, string value) => d.SetValue(SlotProperty, value);

    // Hover = 놓을 수 있는 칸 위에 올라와 있는 동안 True (XAML에서 색을 바꾼다)
    public static readonly DependencyProperty HoverProperty =
        DependencyProperty.RegisterAttached("Hover", typeof(bool), typeof(EquipmentDragDrop),
            new PropertyMetadata(false));

    public static bool GetHover(DependencyObject d) => (bool)d.GetValue(HoverProperty);
    public static void SetHover(DependencyObject d, bool value) => d.SetValue(HoverProperty, value);

    // 마우스를 누른 위치 (조금 움직였을 때만 끌기로 본다)
    private static readonly DependencyProperty StartProperty =
        DependencyProperty.RegisterAttached("Start", typeof(Point), typeof(EquipmentDragDrop));

    // ---------- 끌기 시작하는 쪽 ----------

    private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement f)
            return;

        // 두 번 연결되지 않게 먼저 뺀다
        f.PreviewMouseLeftButtonDown -= Down;
        f.PreviewMouseMove -= Move;
        f.PreviewMouseLeftButtonUp -= Up;

        if ((bool)e.NewValue)
        {
            f.PreviewMouseLeftButtonDown += Down;
            f.PreviewMouseMove += Move;
            f.PreviewMouseLeftButtonUp += Up;
        }
    }

    // 마우스를 누를 때: 시작 위치를 기억한다
    private static void Down(object sender, MouseButtonEventArgs e)
    {
        var f = (FrameworkElement)sender;

        // 카드 안의 "+ 구성" 버튼을 누른 거면 끌기로 보지 않는다
        for (var node = e.OriginalSource as DependencyObject; node != null && node != f; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase)
                return;
        }

        f.SetValue(StartProperty, e.GetPosition(f));
        f.CaptureMouse();
    }

    // 마우스를 뗄 때: 잡고 있던 마우스를 놓는다
    private static void Up(object sender, MouseButtonEventArgs e)
    {
        var f = (FrameworkElement)sender;
        if (f.IsMouseCaptured)
            f.ReleaseMouseCapture();
    }

    // 누른 채로 움직일 때: 일정 거리 이상 움직이면 끌기를 시작한다
    private static void Move(object sender, MouseEventArgs e)
    {
        var f = (FrameworkElement)sender;
        if (e.LeftButton != MouseButtonState.Pressed || !f.IsMouseCaptured || f.DataContext is not StockItem item)
            return;

        var start = (Point)f.GetValue(StartProperty);
        var p = e.GetPosition(f);
        bool movedX = Math.Abs(p.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance;
        bool movedY = Math.Abs(p.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
        if (!movedX && !movedY)
            return;

        f.ReleaseMouseCapture();
        DragDrop.DoDragDrop(f, new DataObject(Format, item), DragDropEffects.Copy);
        e.Handled = true;
    }

    // ---------- 놓는 쪽 (장착 위치) ----------

    private static void SlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement f)
            return;

        f.AllowDrop = e.NewValue != null;

        f.DragOver -= Over;
        f.DragLeave -= Leave;
        f.Drop -= Drop;

        if (f.AllowDrop)
        {
            f.DragOver += Over;
            f.DragLeave += Leave;
            f.Drop += Drop;
        }
    }

    // 창의 DataContext에서 MainViewModel을 꺼낸다
    private static MainViewModel? Vm(FrameworkElement f) => Window.GetWindow(f)?.DataContext as MainViewModel;

    // 칸 위로 끌고 왔을 때: 놓을 수 있는 장비인지 ViewModel에 물어보고 색을 바꾼다
    private static void Over(object sender, DragEventArgs e)
    {
        var f = (FrameworkElement)sender;
        var item = e.Data.GetDataPresent(Format) ? e.Data.GetData(Format) as StockItem : null;
        bool valid = item != null && Vm(f)?.CanStageItem(item, GetSlot(f)) == true;

        SetHover(f, valid);
        e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    // 칸 밖으로 나갔을 때: 색을 원래대로
    private static void Leave(object sender, DragEventArgs e) => SetHover((FrameworkElement)sender, false);

    // 놓았을 때: ViewModel에 장비를 담아 달라고 한다 (아직 확정 전)
    private static void Drop(object sender, DragEventArgs e)
    {
        var f = (FrameworkElement)sender;
        SetHover(f, false);

        if (e.Data.GetDataPresent(Format) && e.Data.GetData(Format) is StockItem item)
            Vm(f)?.StageItem(item, GetSlot(f));

        e.Handled = true;
    }
}
