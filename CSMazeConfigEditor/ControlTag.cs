using System.Windows;
using System.Windows.Controls;

namespace CSMaze.ConfigEditor
{
    internal class ControlTag : DependencyObject
    {
        public static readonly DependencyProperty HeaderLabelProperty = DependencyProperty.Register("HeaderLabel", typeof(Label), typeof(ControlTag));
        public Label HeaderLabel
        {
            get => (Label)GetValue(HeaderLabelProperty);
            set => SetValue(HeaderLabelProperty, value);
        }

        public static readonly DependencyProperty ConfigOptionProperty = DependencyProperty.Register("ConfigOption", typeof(string), typeof(ControlTag));
        public string ConfigOption
        {
            get => (string)GetValue(ConfigOptionProperty);
            set => SetValue(ConfigOptionProperty, value);
        }
    }
}
