using Avalonia.Controls;
using Avalonia.Input;
using EnoUnityLoader.Ui.ViewModels;

namespace EnoUnityLoader.Ui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Allow dragging the window
        PointerPressed += OnPointerPressed;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        await _viewModel.DisposeAsync();
    }
}
