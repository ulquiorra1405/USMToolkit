using System.Windows;

namespace Toolkit.Views;

public partial class NewManualDialog : Window
{
    public string ManualTitle { get; private set; } = "";
    public string ManualCategoria { get; private set; } = "instalacion";
    public string? ManualTags { get; private set; }

    public NewManualDialog()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    private void OnTitleChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CreateBtn.IsEnabled = !string.IsNullOrWhiteSpace(TitleBox.Text);
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        ManualTitle = TitleBox.Text.Trim();
        if (CategoryCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tag)
            ManualCategoria = tag;
        ManualTags = TagsBox.Text.Trim();
        if (string.IsNullOrEmpty(ManualTags))
            ManualTags = null;

        DialogResult = true;
        Close();
    }
}
