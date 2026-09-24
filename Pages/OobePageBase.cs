using System.Windows.Controls;
using FuckETS.Models;

namespace FuckETS.Pages;

/// <summary>OOBE 阶段页面的基类，提供会话、可继续状态与校验钩子。</summary>
public abstract class OobePageBase : Page
{
    /// <summary>可继续状态变化事件（用于更新窗口“下一步”按钮）。</summary>
    public event EventHandler<bool>? CanProceedChanged;

    private bool _canProceed;

    /// <summary>会话共享状态。</summary>
    protected OobeSession Session { get; private set; } = new();

    /// <summary>所属 OOBE 窗口。</summary>
    protected OobeWindow OwnerWindow { get; private set; } = null!;

    /// <summary>当前阶段是否可进入下一步。</summary>
    public bool CanProceed
    {
        get => _canProceed;
        protected set
        {
            if (_canProceed != value)
            {
                _canProceed = value;
                CanProceedChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>页面上生命周期初始化（清理旧状态）。</summary>
    public virtual void OnPageClosed()
    {
    }

    /// <summary>初始化页面。由窗口在导航时调用。</summary>
    public void Initialize(OobeSession session, OobeWindow owner)
    {
        Session = session;
        OwnerWindow = owner;
        OnInitialized();
    }

    /// <summary>子类初始化钩子。</summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>提交页面状态到会话并校验是否可继续。返回 false 时阻止下一步。</summary>
    public abstract bool ValidateAndCommit();
}