using System.Text.Json;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

/// <summary>
/// Service for managing print style settings with persistence
/// </summary>
public class PrintStyleSettingsService
{
    private const string SettingsFileName = "print_style_settings.json";
    private readonly string _settingsFilePath;
    private PrintStyleSettings? _cachedSettings;

    public PrintStyleSettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "PrinterAPP");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, SettingsFileName);
    }

    /// <summary>
    /// Loads settings from disk, returns defaults if file doesn't exist
    /// </summary>
    public PrintStyleSettings LoadSettings()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<PrintStyleSettings>(json);
                if (_cachedSettings != null)
                    return _cachedSettings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading print style settings: {ex.Message}");
        }

        // Return defaults if loading failed
        _cachedSettings = PrintStyleSettings.GetDefaults();
        return _cachedSettings;
    }

    /// <summary>
    /// Saves settings to disk
    /// </summary>
    public void SaveSettings(PrintStyleSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving print style settings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Resets settings to factory defaults
    /// </summary>
    public void ResetToDefaults()
    {
        var defaults = PrintStyleSettings.GetDefaults();
        SaveSettings(defaults);
    }

    /// <summary>
    /// Converts a SectionStyle to ESC/POS command strings
    /// </summary>
    public (string sizeCommand, string boldCommand, string alignCommand) GetEscPosCommands(SectionStyle style)
    {
        // Size commands
        var sizeCommand = style.Size switch
        {
            FontSize.Normal => "\x1D\x21\x00",
            FontSize.Tall => "\x1D\x21\x01",
            FontSize.Wide => "\x1D\x21\x10",
            FontSize.Double => "\x1D\x21\x11",
            FontSize.Large => "\x1D\x21\x22",
            _ => "\x1D\x21\x00"
        };

        // Bold/Emphasis commands
        var boldCommand = "";
        if (style.IsBold)
            boldCommand += "\x1B\x45\x01"; // Bold ON
        if (style.IsEmphasized)
            boldCommand += "\x1B\x47\x01"; // Emphasized ON

        // Alignment command
        var alignCommand = style.Alignment switch
        {
            TextAlignment.Left => "\x1B\x61\x00",
            TextAlignment.Center => "\x1B\x61\x01",
            TextAlignment.Right => "\x1B\x61\x02",
            _ => "\x1B\x61\x00"
        };

        return (sizeCommand, boldCommand, alignCommand);
    }

    /// <summary>
    /// Gets the "turn off" commands for a style to reset formatting
    /// </summary>
    public string GetResetCommands(SectionStyle style)
    {
        var reset = "\x1D\x21\x00"; // Reset to normal size

        if (style.IsBold)
            reset += "\x1B\x45\x00"; // Bold OFF
        if (style.IsEmphasized)
            reset += "\x1B\x47\x00"; // Emphasized OFF

        return reset;
    }
}
