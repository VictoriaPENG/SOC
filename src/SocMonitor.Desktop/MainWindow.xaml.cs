using System.Windows;
using SocMonitor.Desktop.ViewModels;

namespace SocMonitor.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
