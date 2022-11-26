using IniParser;
using IniParser.Model;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CSMaze.ConfigEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string configIniPath;
        private readonly KeyDataCollection configOptions;

        public MainWindow() : this(null) { }

        public MainWindow(string? configIniPath)
        {
            this.configIniPath = configIniPath is not null ? configIniPath : "config.ini";
            configOptions = File.Exists(this.configIniPath) ? new FileIniDataParser().ReadFile(this.configIniPath)["OPTIONS"] : new KeyDataCollection();
            InitializeComponent();
            ((ControlTag)displayColumnsSlider.Tag).DefaultValue = configOptions.ContainsKey("VIEWPORT_WIDTH")
                ? double.Parse(configOptions["VIEWPORT_WIDTH"]) : ((ControlTag)viewportWidthSlider.Tag).DefaultValue;
            foreach (UIElement child in basicPanel.Children.OfType<UIElement>().Concat(advancedPanel.Children.OfType<UIElement>()))
            {
                if (child.GetType() == typeof(Slider))
                {
                    Slider sld = (Slider)child;
                    string configOption = ((ControlTag)sld.Tag).ConfigOption;
                    sld.Value = configOptions.ContainsKey(configOption) ? double.Parse(configOptions[configOption] == "" ? "-0.01" : configOptions[configOption])
                        : ((ControlTag)sld.Tag).DefaultValue;
                    // Force update function to fire even if value was the same
                    Slider_ValueChanged(sld, new RoutedPropertyChangedEventArgs<double>(sld.Value, sld.Value));
                }
                else if (child.GetType() == typeof(CheckBox))
                {
                    CheckBox cbx = (CheckBox)child;
                    string configOption = (string)cbx.Tag;
                    if (configOptions.ContainsKey(configOption))
                    {
                        cbx.IsChecked = configOptions[configOption] != "0";
                    }
                }
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (((Slider)sender).Tag is not null)
            {
                if (sender == viewportWidthSlider && displayColumnsSlider is not null)
                {
                    int newWidth = (int)e.NewValue;
                    // Display columns must always be less than or equal to view width.
                    displayColumnsSlider.Maximum = newWidth;
                }
                ControlTag tag = (ControlTag)((Slider)sender).Tag;
                // Truncate the number of decimal places on a float represented as a string.
                // If the float is negative, it will be converted to an empty string to represent None.
                string toStore = e.NewValue >= 0 ? Math.Round(e.NewValue, tag.DecimalPlaces).ToString() : "";
                // INI files can only contain strings
                configOptions[tag.ConfigOption] = toStore;
                Label headerLabel = (Label)FindName(tag.HeaderLabel);
                headerLabel.Content = (string)headerLabel.Tag + $" — ({(toStore == "" ? "None" : toStore)})";
            }
        }

        private void Check_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cbSender = (CheckBox)sender;
            configOptions[(string)cbSender.Tag] = cbSender.IsChecked is not null && cbSender.IsChecked.Value ? "1" : "0";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            IniData data = new();
            _ = data.Sections.AddSection("OPTIONS");
            data["OPTIONS"].Merge(configOptions);
            new FileIniDataParser().WriteFile(configIniPath, data);
        }

        private int ParseInt(string fieldName, int defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return int.TryParse(value, out int intValue) ? intValue : defaultValue;
        }

        private float ParseFloat(string fieldName, float defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return float.TryParse(value, out float floatValue) ? floatValue : defaultValue;
        }

        private float? ParseOptionalFloat(string fieldName, float? defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return value == "" ? null : int.TryParse(value, out int floatValue) ? floatValue : defaultValue;
        }

        private bool ParseBool(string fieldName, bool defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return value != "0";
        }
    }
}
