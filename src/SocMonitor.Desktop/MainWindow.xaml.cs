using System.Windows;
using SocMonitor.Desktop.ViewModels;

namespace SocMonitor.Desktop;

/// <summary>
/// 主窗口代码隐藏。
/// 当前项目采用简单 MVVM 结构，窗口只负责初始化 XAML 并挂接 MainViewModel。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 所有按钮命令、菜单选项、状态文本和示波器模型都从 MainViewModel 绑定。
        DataContext = new MainViewModel();
    }
}
