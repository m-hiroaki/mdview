using Avalonia.Controls;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}