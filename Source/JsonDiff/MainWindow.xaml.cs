using System.ComponentModel;
using System.Globalization;
using System.Windows;
using JsonDiff.Models;
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
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<string> _startupFiles;

    public MainWindow(ISettingsService settingsService, AppSettings settings, IReadOnlyList<string> startupFiles)
    {
        _settingsService = settingsService;
        _settings = settings;
        _startupFiles = startupFiles;

        InitializeComponent();
        DataContext = _viewModel =
            new MainViewModel(new WpfDialogService(this), new JsonFileLoader(), new MatchRulesFileLoader());
        RestorePlacement(settings);
        _viewModel.ShowChangesOnly = settings.ShowChangesOnly;
        if (settings.ContextLines is int contextLines)
            _viewModel.ContextLinesText = contextLines.ToString(CultureInfo.InvariantCulture);
        if (settings.ShowOverview is bool showOverview)
            _viewModel.ShowOverview = showOverview;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        // Loaded rather than the constructor, so load errors are shown with the window as their owner.
        _viewModel.LoadMatchRules();
        if (_startupFiles.Count > 0)
            _viewModel.LoadFiles(_startupFiles);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            SavePlacement(_settings);
            _settings.ShowChangesOnly = _viewModel.ShowChangesOnly;
            _settings.ContextLines = _viewModel.ContextLines;
            _settings.ShowOverview = _viewModel.ShowOverview;
            _settingsService.Save(_settings);
        }
    }

    private void RestorePlacement(AppSettings settings)
    {
        if (settings.WindowLeft is not double left || settings.WindowTop is not double top ||
            settings.WindowWidth is not double width || settings.WindowHeight is not double height)
            return;

        // Ignore stale bounds that no longer touch any monitor (e.g. a display was unplugged).
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!virtualScreen.IntersectsWith(new Rect(left, top, width, height)))
            return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SavePlacement(AppSettings settings)
    {
        // When maximized or minimized, RestoreBounds holds the normal-state geometry.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        settings.WindowLeft = bounds.Left;
        settings.WindowTop = bounds.Top;
        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
    }
}
