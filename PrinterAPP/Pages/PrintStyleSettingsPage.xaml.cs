using PrinterAPP.Models;
using PrinterAPP.Services;

namespace PrinterAPP.Pages;

public partial class PrintStyleSettingsPage : ContentPage
{
    private readonly PrintStyleSettingsService _settingsService;
    private PrintStyleSettings _currentSettings;

    public PrintStyleSettingsPage()
    {
        InitializeComponent();
        _settingsService = new PrintStyleSettingsService();
        _currentSettings = _settingsService.LoadSettings();
        
        BuildStyleControls();
    }

    private void BuildStyleControls()
    {
        // Kitchen sections
        BuildSectionControls(KitchenHeaderControls, _currentSettings.KitchenHeader);
        BuildSectionControls(KitchenOrderInfoControls, _currentSettings.KitchenOrderInfo);
        BuildSectionControls(KitchenOrderTypeControls, _currentSettings.KitchenOrderType);
        BuildSectionControls(KitchenItemNameControls, _currentSettings.KitchenItemName);
        BuildSectionControls(KitchenItemQuantityControls, _currentSettings.KitchenItemQuantity);
        BuildSectionControls(KitchenIngredientsControls, _currentSettings.KitchenIngredients);

        // Cashier sections
        BuildSectionControls(CashierHeaderControls, _currentSettings.CashierHeader);
        BuildSectionControls(CashierOrderInfoControls, _currentSettings.CashierOrderInfo);
        BuildSectionControls(CashierItemLineControls, _currentSettings.CashierItemLine);
        BuildSectionControls(CashierTotalsControls, _currentSettings.CashierTotals);
        BuildSectionControls(CashierGrandTotalControls, _currentSettings.CashierGrandTotal);
    }

    private void BuildSectionControls(Grid container, SectionStyle style)
    {
        container.RowDefinitions.Clear();
        container.ColumnDefinitions.Clear();
        container.Children.Clear();

        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        // Font Size
        var sizeLabel = new Label { Text = "Font Size:", VerticalOptions = LayoutOptions.Center };
        var sizePicker = new Picker 
        {
            Title = "Select Size",
            ItemsSource = Enum.GetNames(typeof(FontSize)).ToList(),
            SelectedIndex = (int)style.Size
        };
        sizePicker.SelectedIndexChanged += (s, e) => style.Size = (FontSize)sizePicker.SelectedIndex;
        
        Grid.SetRow(sizeLabel, 0);
        Grid.SetColumn(sizeLabel, 0);
        Grid.SetRow(sizePicker, 0);
        Grid.SetColumn(sizePicker, 1);
        container.Children.Add(sizeLabel);
        container.Children.Add(sizePicker);

        // Bold
        var boldLabel = new Label { Text = "Bold:", VerticalOptions = LayoutOptions.Center };
        var boldSwitch = new Switch { IsToggled = style.IsBold };
        boldSwitch.Toggled += (s, e) => style.IsBold = e.Value;
        
        Grid.SetRow(boldLabel, 1);
        Grid.SetColumn(boldLabel, 0);
        Grid.SetRow(boldSwitch, 1);
        Grid.SetColumn(boldSwitch, 1);
        container.Children.Add(boldLabel);
        container.Children.Add(boldSwitch);

        // Emphasized
        var empLabel = new Label { Text = "Emphasized:", VerticalOptions = LayoutOptions.Center };
        var empSwitch = new Switch { IsToggled = style.IsEmphasized };
        empSwitch.Toggled += (s, e) => style.IsEmphasized = e.Value;
        
        Grid.SetRow(empLabel, 2);
        Grid.SetColumn(empLabel, 0);
        Grid.SetRow(empSwitch, 2);
        Grid.SetColumn(empSwitch, 1);
        container.Children.Add(empLabel);
        container.Children.Add(empSwitch);

        // Alignment
        var alignLabel = new Label { Text = "Alignment:", VerticalOptions = LayoutOptions.Center };
        var alignPicker = new Picker
        {
            Title = "Select Alignment",
            ItemsSource = Enum.GetNames(typeof(TextAlignment)).ToList(),
            SelectedIndex = (int)style.Alignment
        };
        alignPicker.SelectedIndexChanged += (s, e) => style.Alignment = (TextAlignment)alignPicker.SelectedIndex;
        
        Grid.SetRow(alignLabel, 3);
        Grid.SetColumn(alignLabel, 0);
        Grid.SetRow(alignPicker, 3);
        Grid.SetColumn(alignPicker, 1);
        container.Children.Add(alignLabel);
        container.Children.Add(alignPicker);
    }

    private void OnKitchenTabClicked(object sender, EventArgs e)
    {
        KitchenSettings.IsVisible = true;
        CashierSettings.IsVisible = false;
        KitchenTabButton.BackgroundColor = (Color)Resources["Primary"];
        KitchenTabButton.TextColor = Colors.White;
        CashierTabButton.BackgroundColor = (Color)Resources["Gray300"];
        CashierTabButton.TextColor = (Color)Resources["Gray900"];
    }

    private void OnCashierTabClicked(object sender, EventArgs e)
    {
        KitchenSettings.IsVisible = false;
        CashierSettings.IsVisible = true;
        CashierTabButton.BackgroundColor = (Color)Resources["Primary"];
        CashierTabButton.TextColor = Colors.White;
        KitchenTabButton.BackgroundColor = (Color)Resources["Gray300"];
        KitchenTabButton.TextColor = (Color)Resources["Gray900"];
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            _settingsService.SaveSettings(_currentSettings);
            await DisplayAlert("Success", "Print style settings saved successfully!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
        }
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Confirm Reset",
            "Are you sure you want to reset all print styles to factory defaults?",
            "Yes",
            "No");

        if (confirm)
        {
            _settingsService.ResetToDefaults();
            _currentSettings = _settingsService.LoadSettings();
            BuildStyleControls();
            await DisplayAlert("Success", "Print styles reset to defaults!", "OK");
        }
    }
}
