using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopManager.ViewModels;

namespace DesktopManager.Views;

/// <summary>
/// 侧边栏 Sheet 项的长按拖拽手势，挂在侧边栏的 <see cref="ItemsControl"/> 上
/// （鼠标事件从按钮隧道 / 冒泡到这里，一个处理器覆盖所有项）：
/// <list type="bullet">
/// <item>按住 <see cref="HoldMs"/> ms 进入拖拽态：“+” 变回收站，并出现跟手幽灵；</item>
/// <item>松手在回收站上 → 请求移除该 Sheet；</item>
/// <item>松手在另一个可拖拽项上 → 请求交换两者位置（目标项画绿色描边）；</item>
/// <item>仍在原项上 → 请求编辑；其它位置 → 取消手势；</item>
/// <item>没到 <see cref="HoldMs"/> 就松手 → 完全不干预，仍是普通的点击选中。</item>
/// </list>
/// </summary>
public static class SidebarDragBehavior
{
    /// <summary>长按进入拖拽态的时长（比 Windows 默认双击时长 500 ms 长，避免误触）。</summary>
    private const int HoldMs = 700;

    /// <summary>
    /// 幽灵矩形的边长（DIP）。VisualBrush 不会把整块 44 DIP 按钮画出来，
    /// 实测绘制内容是 26×26 DIP 且正好以矩形中心对齐光标，所以矩形取 44、
    /// 居中按矩形中心算 —— 改成 26 反而会让内容缩小并偏离光标（已实测）。
    /// </summary>
    private const double GhostSize = 44;

    public static readonly DependencyProperty HostProperty = DependencyProperty.RegisterAttached(
        "Host",
        typeof(SidebarViewModel),
        typeof(SidebarDragBehavior),
        new PropertyMetadata(OnHostChanged));

    public static void SetHost(DependencyObject element, SidebarViewModel value) => element.SetValue(HostProperty, value);

    public static SidebarViewModel GetHost(DependencyObject element) => (SidebarViewModel)element.GetValue(HostProperty);

    /// <summary>“+” 按钮本体：拖拽态下它变成回收站，也是命中测试的目标。</summary>
    public static readonly DependencyProperty RecycleTargetProperty = DependencyProperty.RegisterAttached(
        "RecycleTarget",
        typeof(FrameworkElement),
        typeof(SidebarDragBehavior),
        new PropertyMetadata());

    public static void SetRecycleTarget(DependencyObject element, FrameworkElement value) => element.SetValue(RecycleTargetProperty, value);

    public static FrameworkElement GetRecycleTarget(DependencyObject element) => (FrameworkElement)element.GetValue(RecycleTargetProperty);

    /// <summary>
    /// 拖拽态下指针正压着的交换目标。样式（<c>SidebarButtonStyle</c>）用它给目标项画绿色描边 ——
    /// 鼠标被 ItemsControl 捕获后 <c>IsMouseOver</c> 不再更新，只能由手势按几何判定后写在这里。
    /// </summary>
    public static readonly DependencyProperty IsDropTargetProperty = DependencyProperty.RegisterAttached(
        "IsDropTarget",
        typeof(bool),
        typeof(SidebarDragBehavior),
        new PropertyMetadata(false));

    public static void SetIsDropTarget(DependencyObject element, bool value) => element.SetValue(IsDropTargetProperty, value);

    public static bool GetIsDropTarget(DependencyObject element) => (bool)element.GetValue(IsDropTargetProperty);

    private static readonly ConditionalWeakTable<ItemsControl, Gesture> Gestures = new();

    private static void OnHostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl items)
        {
            return;
        }

        if (Gestures.TryGetValue(items, out var existing))
        {
            existing.Detach();
            Gestures.Remove(items);
        }

        if (e.NewValue is SidebarViewModel)
        {
            Gestures.Add(items, new Gesture(items));
        }
    }

    /// <summary>一个 ItemsControl 对应一个手势实例：同一时刻只可能有一个手势在跑。</summary>
    private sealed class Gesture
    {
        private readonly ItemsControl _items;
        private readonly DispatcherTimer _timer;

        private Button? _source;
        private NavItemViewModel? _item;
        private FrameworkElement? _root;
        private FrameworkElement? _ghostHost;
        private GhostAdorner? _ghost;
        private Button? _target;
        private bool _dragging;

        public Gesture(ItemsControl items)
        {
            _items = items;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldMs) };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                Arm();
            };

            items.PreviewMouseLeftButtonDown += OnDown;
            items.PreviewMouseMove += OnMove;
            items.PreviewMouseLeftButtonUp += OnUp;
        }

        public void Detach()
        {
            _timer.Stop();
            _items.PreviewMouseLeftButtonDown -= OnDown;
            _items.PreviewMouseLeftButtonUp -= OnUp;
            _items.PreviewMouseMove -= OnMove;
            Reset();
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            // 上一轮万一没清理干净，先清掉，避免幽灵和回收站卡住
            Reset();

            var button = FindButton(e.OriginalSource as DependencyObject);
            if (button is null || button.DataContext is not NavItemViewModel item || !item.CanDrag)
            {
                // 内置“桌面”项、设置项、空白处：完全不干预，保持普通点击
                return;
            }

            _source = button;
            _item = item;
            _timer.Start();
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _root is null)
            {
                return;
            }

            var p = e.GetPosition(_root);
            _ghost?.Move(_ghostHost is null ? p : e.GetPosition(_ghostHost));

            var host = GetHost(_items);
            if (host is not null)
            {
                // 鼠标被捕获后其它元素的 IsMouseOver 不再更新，所以回收站高亮只能自己按位置算
                host.IsRecycleHot = Over(GetRecycleTarget(_items), p);
            }

            // 交换目标同理：按几何找指针下面的另一个可拖拽项，样式据此画描边
            SetTarget(HitTestItem(p));
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            _timer.Stop();

            if (!_dragging)
            {
                // 没到长按阈值：交回给按钮的 Click（选中该 Sheet）
                _source = null;
                _item = null;
                return;
            }

            // 手势已经接管，不能再让按钮把这一项选中
            e.Handled = true;

            var host = GetHost(_items);
            var item = _item;
            var p = _root is null ? new Point(-1, -1) : e.GetPosition(_root);
            var overRecycle = Over(GetRecycleTarget(_items), p);
            var overSource = Over(_source, p);

            // 交换目标：再按当前位置复核一次，避免只有 Move 事件滞后时误交换
            var swapWith = _target is not null && Over(_target, p) ? _target.DataContext as NavItemViewModel : null;

            Reset();

            if (host is null || item is null)
            {
                return;
            }

            if (overRecycle)
            {
                host.RequestRemoveSheet(item);
            }
            else if (swapWith is not null)
            {
                host.RequestSwapSheets(item, swapWith);
            }
            else if (overSource)
            {
                host.RequestEditSheet(item);
            }
            // 其它位置：取消手势，什么都不做
        }

        private void Arm()
        {
            if (_source is null || _item is null)
            {
                return;
            }

            _dragging = true;

            // 捕获鼠标：指针移出侧边栏也一定能收到 Move / Up
            _items.CaptureMouse();

            _root = Window.GetWindow(_items);
            _ghostHost = FindAdornerHost(_items);
            if (_ghostHost is not null)
            {
                var layer = AdornerLayer.GetAdornerLayer(_ghostHost);
                if (layer is not null)
                {
                    _ghost = new GhostAdorner(_ghostHost, _source);
                    layer.Add(_ghost);
                }
            }

            var host = GetHost(_items);
            if (host is not null)
            {
                host.IsRecycleVisible = true;
            }

            if (_root is not null && _ghostHost is not null)
            {
                _ghost?.Move(Mouse.GetPosition(_ghostHost));
            }
        }

        /// <summary>
        /// 找最靠上、仍能解析出 AdornerLayer 的元素：窗口根 Border。
        /// 直接对 Window 调 GetAdornerLayer 会得到 null（AdornerLayer 在窗口模板的
        /// AdornerDecorator 里，只有它下面的元素才找得到），幽灵就会静默加不上。
        /// </summary>
        private static FrameworkElement? FindAdornerHost(DependencyObject start)
        {
            FrameworkElement? best = null;
            var node = start;

            while (node is not null)
            {
                if (node is FrameworkElement element && AdornerLayer.GetAdornerLayer(element) is not null)
                {
                    best = element;
                }

                node = VisualTreeHelper.GetParent(node);
            }

            return best;
        }

        /// <summary>
        /// 指针下面的交换目标：跳过源项与不可拖拽项（内置“桌面”、设置）。
        /// 回收站按钮不在 ItemsControl 里，所以永远不会被这里命中。
        /// </summary>
        private Button? HitTestItem(Point p)
        {
            foreach (var button in EnumerateButtons())
            {
                if (ReferenceEquals(button, _source))
                {
                    continue;
                }

                if (button.DataContext is not NavItemViewModel item || !item.CanDrag)
                {
                    continue;
                }

                if (Over(button, p))
                {
                    return button;
                }
            }

            return null;
        }

        /// <summary>ItemsControl 里全部按钮（容器没虚拟化，直接走视觉树）。</summary>
        private IEnumerable<Button> EnumerateButtons()
        {
            var pending = new Stack<DependencyObject>();
            pending.Push(_items);

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                var count = VisualTreeHelper.GetChildrenCount(node);
                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    if (child is Button button)
                    {
                        yield return button;
                    }

                    pending.Push(child);
                }
            }
        }

        /// <summary>切换高亮目标：旧目标清掉描边，新目标写上描边。</summary>
        private void SetTarget(Button? target)
        {
            if (ReferenceEquals(target, _target))
            {
                return;
            }

            if (_target is not null)
            {
                SetIsDropTarget(_target, false);
            }

            _target = target;

            if (_target is not null)
            {
                SetIsDropTarget(_target, true);
            }
        }

        private void Reset()
        {
            _dragging = false;
            _timer.Stop();

            SetTarget(null);

            if (_items.IsMouseCaptured)
            {
                _items.ReleaseMouseCapture();
            }

            if (_ghost is not null)
            {
                AdornerLayer.GetAdornerLayer(_ghostHost ?? _items)?.Remove(_ghost);
                _ghost = null;
            }

            var host = GetHost(_items);
            if (host is not null)
            {
                host.IsRecycleVisible = false;
                host.IsRecycleHot = false;
            }

            _source = null;
            _item = null;
        }

        /// <summary>从事件源往上找承载 NavItemViewModel 的按钮；走到 ItemsControl 还没找到就是点在空白处。</summary>
        private Button? FindButton(DependencyObject? node)
        {
            while (node is not null && node != _items)
            {
                if (node is Button button)
                {
                    return button;
                }

                if (node is not Visual)
                {
                    return null;
                }

                node = VisualTreeHelper.GetParent(node);
            }

            return null;
        }

        /// <summary>点是否落在某个元素的渲染矩形内（坐标统一换算到窗口根，DPI 安全）。</summary>
        private static bool Over(FrameworkElement? element, Point p)
        {
            if (element is null || element.RenderSize.Width <= 0 || element.RenderSize.Height <= 0)
            {
                return false;
            }

            var root = Window.GetWindow(element);
            if (root is null)
            {
                return false;
            }

            var origin = element.TranslatePoint(new Point(0, 0), root);
            return new Rect(origin, element.RenderSize).Contains(p);
        }
    }

    /// <summary>
    /// 跟手幽灵：把被拖按钮的视觉原样画到光标上（自定义图标与内置字形都自动跟着走）。
    /// 挂在窗口根的 AdornerLayer 上，覆盖整个窗口且不吞命中。
    /// </summary>
    private sealed class GhostAdorner : Adorner
    {
        private readonly Rectangle _tile;
        private Point _position;

        public GhostAdorner(UIElement adornedElement, Visual source)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            _tile = new Rectangle
            {
                Width = GhostSize,
                Height = GhostSize,
                Opacity = 0.7,
                IsHitTestVisible = false,
                Fill = new VisualBrush(source)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                },
            };

            AddVisualChild(_tile);
        }

        public void Move(Point position)
        {
            _position = new Point(position.X - GhostSize / 2, position.Y - GhostSize / 2);
            InvalidateArrange();
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _tile;

        protected override Size MeasureOverride(Size constraint)
        {
            _tile.Measure(new Size(GhostSize, GhostSize));
            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _tile.Arrange(new Rect(_position, new Size(GhostSize, GhostSize)));
            return finalSize;
        }
    }
}
