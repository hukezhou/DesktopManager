using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DesktopManager.Views;

/// <summary>
/// 附加属性：让列表的滚动条“空闲时淡出”。
/// 鼠标在列表内、或刚刚滚动过（400ms 内）时 <c>IsRevealed</c> 为 true，其余为 false；
/// 滚动条样式绑定这个值做淡入淡出。
/// </summary>
public static class ScrollRevealBehavior
{
    /// <summary>停止滚动后还要保持可见的时间。</summary>
    private const int RevealHoldMs = 400;

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(ScrollRevealBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyPropertyKey IsRevealedPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsRevealed",
        typeof(bool),
        typeof(ScrollRevealBehavior),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsRevealedProperty = IsRevealedPropertyKey.DependencyProperty;

    /// <summary>挂状态对象用的私有属性。</summary>
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(State),
        typeof(ScrollRevealBehavior),
        new PropertyMetadata());

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static bool GetIsRevealed(DependencyObject element) => (bool)element.GetValue(IsRevealedProperty);

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ListBox list)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            Hook(list);
        }
        else
        {
            Unhook(list);
        }
    }

    private static void Hook(ListBox list)
    {
        if (list.GetValue(StateProperty) is not null)
        {
            return;
        }

        var state = new State(list);
        list.SetValue(StateProperty, state);

        // ScrollChanged 是冒泡事件，列表本身就能收到内部 ScrollViewer 的滚动通知，
        // 不必去可视树里找 ScrollViewer
        list.AddHandler(ScrollViewer.ScrollChangedEvent, state.OnScrollChanged);
        list.MouseEnter += state.OnEnter;
        list.MouseLeave += state.OnLeave;

        SetRevealed(list, list.IsMouseOver);
    }

    private static void Unhook(ListBox list)
    {
        if (list.GetValue(StateProperty) is not State state)
        {
            return;
        }

        list.RemoveHandler(ScrollViewer.ScrollChangedEvent, state.OnScrollChanged);
        list.MouseEnter -= state.OnEnter;
        list.MouseLeave -= state.OnLeave;
        state.StopTimer();
        list.SetValue(StateProperty, null);
    }

    private static void SetRevealed(ListBox list, bool revealed)
    {
        if (GetIsRevealed(list) != revealed)
        {
            list.SetValue(IsRevealedPropertyKey, revealed);
        }
    }

    /// <summary>每个列表一份：事件委托（便于解绑）+ 保持可见的计时器。</summary>
    private sealed class State
    {
        private readonly ListBox _list;
        private DispatcherTimer? _timer;

        public State(ListBox list)
        {
            _list = list;
            // 委托在这里建，解绑时能拿到同一个实例
            OnScrollChanged = (_, _) => RevealBriefly();
            OnEnter = (_, _) => Enter();
            OnLeave = (_, _) => Leave();
        }

        public ScrollChangedEventHandler OnScrollChanged { get; }

        public MouseEventHandler OnEnter { get; }

        public MouseEventHandler OnLeave { get; }

        private void Enter()
        {
            StopTimer();
            SetRevealed(_list, true);
        }

        private void Leave()
        {
            StopTimer();
            SetRevealed(_list, false);
        }

        private void RevealBriefly()
        {
            SetRevealed(_list, true);

            _timer ??= new DispatcherTimer(
                TimeSpan.FromMilliseconds(RevealHoldMs),
                DispatcherPriority.Background,
                (_, _) => Tick(),
                _list.Dispatcher);

            _timer.Stop();
            _timer.Start();
        }

        private void Tick()
        {
            _timer?.Stop();
            // 手还停在列表里（比如刚拖完滑块）就继续显示
            SetRevealed(_list, _list.IsMouseOver);
        }

        public void StopTimer() => _timer?.Stop();
    }
}
