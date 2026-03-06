using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WatchMark.App.Models;
using WatchMark.App.ViewModels;
using WinForms = System.Windows.Forms;

namespace WatchMark.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow Constructor Error: {ex}");
            System.Windows.MessageBox.Show($"Failed to initialize window:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}\n\nStack:\n{ex.StackTrace}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private void LibraryPathComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Force scan when user selects from dropdown (even if it's the same path)
        if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is string selectedPath && DataContext is MainViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine($"LibraryPathComboBox_SelectionChanged: selectedPath='{selectedPath}'");
            
            if (!string.IsNullOrWhiteSpace(selectedPath) && System.IO.Directory.Exists(selectedPath))
            {
                // Force update the binding
                viewModel.SelectedLibraryPath = selectedPath;
                viewModel.AddRecentPath(selectedPath);
                
                // Force a scan directly (bypasses the property change check)
                System.Diagnostics.Debug.WriteLine($"LibraryPathComboBox_SelectionChanged: Forcing scan for '{selectedPath}'");
                viewModel.ScanLibrary();
            }
        }
    }

    private void MoviesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var clickedElement = e.OriginalSource as DependencyObject;
            var clickedCheckBox = FindAncestor<System.Windows.Controls.CheckBox>(clickedElement);
            if (clickedCheckBox is not null)
            {
                return;
            }

            var cell = FindAncestor<DataGridCell>(clickedElement);
            if (cell?.Column is DataGridCheckBoxColumn)
            {
                return;
            }

            var row = FindAncestor<DataGridRow>(clickedElement);
            var movie = row?.Item as MovieItem;
            
            if (movie is not null)
            {
                // Open movie with resume support
                viewModel.OpenMovieInVlc(movie);
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
