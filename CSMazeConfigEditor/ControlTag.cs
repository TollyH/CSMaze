using System.Windows;
using System.Windows.Controls;

namespace CSMaze.ConfigEditor
{
    internal class ControlTag : DependencyObject
    {
        public static readonly DependencyProperty HeaderLabelProperty = DependencyProperty.Register("HeaderLabel", typeof(string), typeof(ControlTag));
        public string HeaderLabel
        {
            get => (string)GetValue(HeaderLabelProperty);
            set => SetValue(HeaderLabelProperty, value);
        }

        public static readonly DependencyProperty ConfigOptionProperty = DependencyProperty.Register("ConfigOption", typeof(string), typeof(ControlTag));
        public string ConfigOption
        {
            get => (string)GetValue(ConfigOptionProperty);
            set => SetValue(ConfigOptionProperty, value);
        }

        public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register("DecimalPlaces", typeof(int), typeof(ControlTag));
        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register("DefaultValue", typeof(double), typeof(ControlTag));
        public double DefaultValue
        {
            get => (double)GetValue(DefaultValueProperty);
            set => SetValue(DefaultValueProperty, value);
        }
    }
}
