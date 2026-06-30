using System.Windows;
using AIVoiceCable.ViewModels;

namespace AIVoiceCable;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
