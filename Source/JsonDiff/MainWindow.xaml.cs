using System.Windows;
using JsonDiff.Services;
using JsonDiff.ViewModels;

namespace JsonDiff;

/// <summary>
/// Interaction logic for MainWindow.xaml. Wires the view model with a window-bound dialog service;
/// keyboard shortcuts are declared as InputBindings in the XAML.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IReadOnlyList<string> _startupFiles;

    public MainWindow(IReadOnlyList<string> startupFiles)
    {
        _startupFiles = startupFiles;

        InitializeComponent();
        DataContext = _viewModel = new MainViewModel(new WpfDialogService(this), new JsonFileLoader());
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        // Loaded rather than the constructor, so load errors are shown with the window as their owner.
        if (_startupFiles.Count > 0)
            _viewModel.LoadFiles(_startupFiles);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
