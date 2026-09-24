using System.Windows;
using FuckETS.Services;

namespace FuckETS;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>应用启动路由：校验 OOBE 状态，未完成则进入向导，已完成则进入主界面。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册全局未处理异常日志，避免出现“无日志输出”的静默卡死/崩溃。
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("UI 线程未处理异常。", args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Logger.Error("AppDomain 未处理异常。", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("任务未观察异常。", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);

        // 最早阶段初始化日志记录器，使 OOBE 及后续全程均有日志。
        Logger.Initialize();
        Logger.Info($"应用启动。版本 {GetType().Assembly.GetName().Version}");

        // 若 OOBE 已完成且校验通过，直接进入主界面（不启动扫描等业务逻辑）
        if (TryValidateAndRunMain())
            return;

        // 否则先进入 OOBE 向导
        RunOobe();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info($"应用退出。ExitCode = {e.ApplicationExitCode}");
        base.OnExit(e);
    }

    /// <summary>校验 OOBE：全部有效则打开主界面，返回 true。</summary>
    private bool TryValidateAndRunMain()
    {
        Logger.Debug("正在校验 OOBE 状态…");
        try
        {
            if (OobeStateService.IsCompleted())
            {
                Logger.Info("OOBE 校验通过，直接进入主界面。");
                var main = new MainWindow();
                main.Show();
                return true;
            }
            Logger.Info("OOBE 未完成，需进入初始设置向导。");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("校验 OOBE 状态时发生异常。", ex);
            return false;
        }
    }

    /// <summary>运行 OOBE 向导；完成后会打开主界面。</summary>
    private void RunOobe()
    {
        Logger.Info("启动 OOBE 初始设置向导。");
        var oobe = new OobeWindow();
        oobe.Begin();
        if (oobe.ShowDialog() == true)
        {
            // OOBE 已完成（含中途续做后完成），进入主界面
            Logger.Info("OOBE 完成，进入主界面。");
            var main = new MainWindow();
            main.Show();
        }
        else
        {
            // 用户关闭 OOBE：再次校验，若已完整完成也进入主界面；否则结束
            Logger.Info("OOBE 向导被关闭（未完成）。");
            if (TryValidateAndRunMain())
                return;
            Shutdown();
        }
    }
}