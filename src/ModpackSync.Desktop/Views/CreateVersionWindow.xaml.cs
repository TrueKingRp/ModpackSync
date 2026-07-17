using System.Windows;
using System.Windows.Controls;

namespace ModpackSync.Desktop.Views;

public partial class CreateVersionWindow : Window
{
    private readonly HashSet<string> _existingVersionLabels;

    public string VersionLabel =>
        VersionLabelTextBox.Text.Trim();

    public string ReleaseNotes =>
        ReleaseNotesTextBox.Text.Trim();

    public CreateVersionWindow(
        string packName,
        IEnumerable<string>? existingVersionLabels = null)
    {
        InitializeComponent();

        PackNameTextBlock.Text =
            $"Create a new local version for {packName}.";

        _existingVersionLabels =
            new HashSet<string>(
                existingVersionLabels
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

        Loaded += CreateVersionWindow_Loaded;

        ValidateForm();
    }

    private void CreateVersionWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        VersionLabelTextBox.Focus();
        VersionLabelTextBox.SelectAll();
    }

    private void VersionLabelTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (CreateButton is null)
        {
            return;
        }

        ValidateForm();
    }

    private void CreateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!ValidateForm())
        {
            return;
        }

        DialogResult = true;
    }

    private bool ValidateForm()
    {
        string versionLabel =
            VersionLabelTextBox.Text.Trim();

        string? validationMessage =
            GetValidationMessage(versionLabel);

        bool isValid =
            validationMessage is null;

        CreateButton.IsEnabled =
            isValid;

        ValidationTextBlock.Text =
            validationMessage ?? string.Empty;

        ValidationTextBlock.Visibility =
            isValid
                ? Visibility.Collapsed
                : Visibility.Visible;

        return isValid;
    }

    private string? GetValidationMessage(
        string versionLabel)
    {
        if (string.IsNullOrWhiteSpace(
                versionLabel))
        {
            return "Enter a version label.";
        }

        if (versionLabel.Length > 100)
        {
            return "The version label cannot exceed 100 characters.";
        }

        if (_existingVersionLabels.Contains(
                versionLabel))
        {
            return $"Version {versionLabel} already exists.";
        }

        char[] invalidCharacters =
        [
            '\\',
            '/',
            ':',
            '*',
            '?',
            '"',
            '<',
            '>',
            '|'
        ];

        if (versionLabel.IndexOfAny(
                invalidCharacters) >= 0)
        {
            return "The version label contains an invalid character.";
        }

        if (versionLabel.EndsWith(
                '.'))
        {
            return "The version label cannot end with a full stop.";
        }

        return null;
    }
}