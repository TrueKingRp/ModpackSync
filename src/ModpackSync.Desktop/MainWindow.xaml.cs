using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModpackSync.Contracts.Packs;
using ModpackSync.Desktop.Views;

namespace ModpackSync.Desktop;

public partial class MainWindow : Window
{
    private readonly DashboardView _dashboardView;
    private readonly SettingsView _settingsView;
    private readonly ServerBrowserView _serverBrowserView;

    public MainWindow()
    {
        InitializeComponent();

        _dashboardView =
            new DashboardView();

        _dashboardView.OpenVersionsRequested +=
            DashboardView_OpenVersionsRequested;

        _settingsView =
            new SettingsView();

        _settingsView.ConnectionStatusChanged +=
            SettingsView_ConnectionStatusChanged;

        _serverBrowserView =
    new ServerBrowserView();

        ShowDashboard();
    }

    private void DashboardNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowDashboard();
    }

    private void ServerNavigationButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        ShowServer();
    }

    private void ShowServer()
    {
        MainContentHost.Content =
            _serverBrowserView;

        SetNavigationSelection(
            ServerNavigationButton);
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

        ShowVersions(
            selectedPack);
    }

    private void SettingsNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowSettings();
    }

    private void DashboardView_OpenVersionsRequested(
        ModpackRegistration pack)
    {
        ShowVersions(
            pack);
    }

    private void SettingsView_ConnectionStatusChanged(
        bool isConnected)
    {
        ServerStatusEllipse.Fill =
            (Brush)FindResource(
                isConnected
                    ? "SuccessBrush"
                    : "ErrorBrush");

        ServerStatusTextBlock.Text =
            isConnected
                ? "Connected"
                : "Not connected";
    }

    private void ShowDashboard()
    {
        MainContentHost.Content =
            _dashboardView;

        SetNavigationSelection(
            DashboardNavigationButton);
    }

    private void ShowVersions(
        ModpackRegistration pack)
    {
        var versionsView =
            new VersionsView(
                pack);

        versionsView.BackRequested +=
            ShowDashboard;

        MainContentHost.Content =
            versionsView;

        SetNavigationSelection(
            VersionsNavigationButton);
    }

    private void ShowSettings()
    {
        MainContentHost.Content =
            _settingsView;

        SetNavigationSelection(
            SettingsNavigationButton);
    }

    private void SetNavigationSelection(
        Button selectedButton)
    {
        Button[] navigationButtons =
        [
            DashboardNavigationButton,
            VersionsNavigationButton,
            ServerNavigationButton,
            SettingsNavigationButton
        ];

        foreach (Button button in navigationButtons)
        {
            button.Background =
                Brushes.Transparent;

            button.Foreground =
                (Brush)FindResource(
                    "SecondaryTextBrush");
        }

        selectedButton.Background =
            (Brush)FindResource(
                "SelectionBrush");

        selectedButton.Foreground =
            (Brush)FindResource(
                "TextBrush");
    }
}