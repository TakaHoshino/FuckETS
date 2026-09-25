using System.IO;
using System.Windows;
using FuckETS.PluginSdk.Models;
using FuckETS.PluginSdk.Packaging;
using FuckETS.Plugins;
using FuckETS.Services;

namespace FuckETS;

/// <summary>插件管理窗口：查看/启停/加载/移除插件。</summary>
public partial class PluginManagerWindow : Window
{
    public PluginManagerWindow()
    {
        InitializeComponent();
        PluginDirLabel.Text = $"插件目录：{PluginHost.GetExternalPluginDirectory()}";
        RefreshList();
    }

    private void RefreshList()
    {
        PluginList.ItemsSource = null;
        PluginList.ItemsSource = PluginHost.Current.Plugins;
    }

    private void LoadPluginButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择插件包",
            Filter = "FuckETS 插件 (*.fep)|*.fep",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        // 先做包校验，给出可读错误
        var read = FepPackager.ReadManifest(dialog.FileName);
        if (!read.Success || read.Manifest is null)
        {
            Logger.Warn($"插件包校验失败：{dialog.FileName}（{read.Error}）。");
            MessageBox.Show($"插件包校验失败：\n{read.Error}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            var pluginDir = PluginHost.GetExternalPluginDirectory();
            Directory.CreateDirectory(pluginDir);
            var target = Path.Combine(pluginDir, Path.GetFileName(dialog.FileName));
            if (!string.Equals(Path.GetFullPath(target), Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase))
                File.Copy(dialog.FileName, target, overwrite: true);

            var info = PluginHost.Current.LoadExternalPlugin(target);
            if (info is null)
            {
                MessageBox.Show("插件加载失败，详见日志（%APPDATA%\\FuckETS\\logs）。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            RefreshList();
            Logger.Info($"插件管理：已加载 {info.DisplayName}。");
        }
        catch (Exception ex)
        {
            Logger.Error($"插件加载异常：{ex.Message}", ex);
            MessageBox.Show($"插件加载异常：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (PluginList.SelectedItem is not PluginInfo info)
            return;
        if (info.BuiltIn)
        {
            MessageBox.Show("内置插件不可移除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show($"确定移除插件「{info.DisplayName}」吗？\n（将同时删除其 .fep 文件）",
            "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        if (PluginHost.Current.RemoveExternalPlugin(info.Manifest.Id))
        {
            RemoveButton.IsEnabled = false;
            RefreshList();
        }
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
            return;
        var info = PluginHost.Current.Plugins.FirstOrDefault(p => p.Manifest.Id == id);
        if (info is null)
            return;
        if (!PluginHost.Current.SetEnabled(info.Manifest.Id, !info.Enabled))
            return;
        RefreshList();
    }

    private void PluginList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RemoveButton.IsEnabled = PluginList.SelectedItem is PluginInfo { BuiltIn: false };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}