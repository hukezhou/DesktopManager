using System.Threading;

namespace DesktopManager.Services;

/// <summary>单实例闸门：同一登录会话内只有第一个进程能取得锁。</summary>
public interface ISingleInstanceGuard : IDisposable
{
    /// <summary>尝试取得单实例锁；返回 false 表示已有实例在运行。</summary>
    bool TryAcquire();
}

/// <summary>
/// 用命名 Mutex 实现单实例。进程崩溃或被强杀时由系统释放，不会留下死锁（这点优于锁文件）。
/// </summary>
public sealed class SingleInstanceGuard : ISingleInstanceGuard
{
    /// <summary>
    /// 刻意不加 Global\ 前缀：普通用户没有 SeCreateGlobalPrivilege，创建 Global\ 内核对象会被拒绝。
    /// 不加前缀即“当前登录会话内唯一”，正好等于每个用户一个实例。
    /// </summary>
    private const string MutexName = "DesktopManager.SingleInstance";

    private Mutex? _mutex;
    private bool _owned;

    public bool TryAcquire()
    {
        if (_owned)
        {
            return true;
        }

        var mutex = new Mutex(initiallyOwned: false, MutexName);

        try
        {
            // 名字已存在时也可能上一个实例正在退出（已释放锁但进程还没结束），
            // 这种“接管”比拒绝启动更合理，所以两种情况都直接试抢一次。
            if (mutex.WaitOne(TimeSpan.Zero))
            {
                _mutex = mutex;
                _owned = true;
                return true;
            }
        }
        catch (AbandonedMutexException)
        {
            // 持有者被强杀：锁已经归我们，按正常启动处理。
            _mutex = mutex;
            _owned = true;
            return true;
        }

        mutex.Dispose();
        return false;
    }

    public void Dispose()
    {
        var mutex = _mutex;
        _mutex = null;

        if (mutex is null)
        {
            return;
        }

        if (_owned)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 已经不是持有者，忽略。
            }
        }

        mutex.Dispose();
        _owned = false;
    }
}
