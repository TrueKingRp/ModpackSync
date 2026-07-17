using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModpackSync.Contracts.Packs;
using ModpackSync.Desktop.Views;

namespace ModpackSync.Desktop;

public partial class MainWindow : Window
{
    private readonly DashboardView _dashboardView;

    public MainWindow()
    {
        InitializeComponent();

        _dashboardView = new DashboardView();
        _dashboardView.OpenVersionsRequested +=
            DashboardView_OpenVersionsRequested;

        ShowDashboard();
    }

    private void DashboardNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowDashboard();
    }

    private void VersionsNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ModpackRegistration? selectedPack =
            _dashboardView.SelectedManagedPack;

        if (selectedPack is null)
        {
            MessageBox.Show(
                "Select a managed pack on the dashboard before opening its versions.",
                "ModpackSync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        ShowVersions(selectedPack);
    }

    private void DashboardView_OpenVersionsRequested(
        ModpackRegistration pack)
    {
        ShowVersions(pack);
    }

    private void ShowDashboard()
    {
        MainContentHost.Content = _dashboardView;

        SetNavigationSelection(
            DashboardNavigationButton);
    }

    private void ShowVersions(
        ModpackRegistration pack)
    {
        var versionsView =
            new VersionsView(pack);

        versionsView.BackRequested +=
            ShowDashboard;

        MainContentHost.Content =
            versionsView;

        SetNavigationSelection(
            VersionsNavigationButton);
    }

    private void SetNavigationSelection(
        Button selectedButton)
    {
        DashboardNavigationButton.Background =
            Brushes.Transparent;

        DashboardNavigationButton.Foreground =
            (Brush)FindResource(
                "SecondaryTextBrush");

        VersionsNavigationButton.Background =
            Brushes.Transparent;

        VersionsNavigationButton.Foreground =
            (Brush)FindResource(
                "SecondaryTextBrush");

        selectedButton.Background =
            (Brush)FindResource(
                "SelectionBrush");

        selectedButton.Foreground =
            (Brush)FindResource(
                "TextBrush");
    }
}