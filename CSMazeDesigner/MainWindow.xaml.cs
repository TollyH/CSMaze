using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CSMaze.Designer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Config cfg;
        private string? currentPath = null;
        private Level[] levels = Array.Empty<Level>();
        private int currentLevel = -1;
        private Tool currentTool = Tool.Select;
        private System.Drawing.Point currentTile = new(-1, -1);
        private List<System.Drawing.Point> bulkWallSelection = new();
        // The mouse must leave a tile before that same tile is modified again.
        private System.Drawing.Point lastVisitedTile = new(-1, -1);
        private double zoomLevel = 1;
        private System.Drawing.Point scrollOffset = new(0, 0);
        private List<(int, Level[])> undoStack = new();
        private bool unsavedChanges = false;
        // Used to prevent methods from being called when programmatically setting widget values.
        private bool doUpdates = true;

        private Dictionary<Tool, string> descriptions = new();
        private Dictionary<Tool, Button> toolButtons = new();
        private Dictionary<string, Image> textures = new();
        private Dictionary<string, Image> decorationTextures = new();

        public MainWindow() : this(null) { }

        public MainWindow(string? configIniPath = "config.ini")
        {
            cfg = new Config(configIniPath ?? "config.ini");
            InitializeComponent();
            foreach (Button btn in toolButtonPanel.Children.OfType<Button>())
            {
                toolButtons[(Tool)btn.Tag] = btn;
            }
        }

        /// <summary>
        /// Change the currently selected tool and update buttons to match.
        /// Silently fails if the specified tool does not exist.
        /// </summary>
        private void SelectTool(Tool newTool)
        {
            if (toolButtons.ContainsKey(newTool))
            {
                toolButtons[currentTool].IsEnabled = true;
                currentTool = newTool;
                toolButtons[currentTool].IsEnabled = false;
            }
        }

        /// <summary>
        /// Prompt the user to select a JSON file then load it, overwriting the data currently loaded.
        /// </summary>
        private void OpenFile()
        {
            if (unsavedChanges && MessageBox.Show("You currently have unsaved changes, are you sure you wish to load a file? This will overwrite everything here.",
                "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            OpenFileDialog dialog = new()
            {
                CheckFileExists = true,
                DefaultExt = "JSON files|*.json",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            bool? result = dialog.ShowDialog();
            if (result is null || !result.Value)
            {
                return;
            }
            if (!File.Exists(dialog.FileName))
            {
                _ = MessageBox.Show("File does not exist", "Not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                levels = MazeLevels.LoadLevelJson(dialog.FileName);
                currentPath = dialog.FileName;
                Title = $"Level Designer - {dialog.FileName}";
                currentLevel = -1;
                currentTile = new System.Drawing.Point(-1, -1);
                bulkWallSelection.Clear();
                zoomLevel = 1;
                scrollOffset = new System.Drawing.Point(0, 0);
                doUpdates = false;
                zoomSlider.Value = 1;
                doUpdates = true;
                undoStack.Clear();
                undoButton.IsEnabled = false;
                unsavedChanges = false;
                // TODO: UpdateLevelList()
                // TODO: UpdateMapCanvas()
                // TODO: UpdatePropertiesPanel()
            }
            catch (Exception exc)
            {
                _ = MessageBox.Show($"An error occurred loading the file.\nIs it definitely a valid levels file?\n\nThe following info was given: {exc}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        /// <summary>
        /// Prompt the user to provide a location to save a JSON file then do so.
        /// </summary>
        /// <param name="filepath">If given, the user file prompt will be skipped.</param>
        private void SaveFile(string? filepath = null)
        {
            if (filepath is null or "")
            {
                SaveFileDialog dialog = new()
                {
                    AddExtension = true,
                    DefaultExt = "JSON files|*.json",
                    InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                bool? result = dialog.ShowDialog();
                if (result is null || !result.Value)
                {
                    return;
                }
                filepath = dialog.FileName;
            }
            if (filepath == "")
            {
                return;
            }
            try
            {
                MazeLevels.SaveLevelJson(filepath, levels);
                Title = $"Level Designer - {filepath}";
                currentPath = filepath;
                unsavedChanges = false;
            }
            catch (Exception exc)
            {
                _ = MessageBox.Show($"An error occurred saving the file.\nThe following info was given: {exc}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            SelectTool((Tool)((Button)sender).Tag);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S)
            {
                SelectTool(currentTool + 1);
            }
            if (e.Key == Key.W)
            {
                SelectTool(currentTool - 1);
            }
            if (e.Key == Key.A)
            {
                // TODO: Bulk select walls
            }
        }

        private void mapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void mapCanvas_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void zoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void DimensionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void monsterWaitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void textureDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void edgeTextureDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void decorationTextureDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void undoButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void openButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFile(currentPath);
        }

        private void saveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFile();
        }

        private void levelAddButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void levelDeleteButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void levelMoveUpButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void levelMoveDownButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public enum Tool
    {
        Select,
        Move,
        Wall,
        CollisionPlayer,
        CollisionMonster,
        Start,
        End,
        Key,
        Sensor,
        Gun,
        Monster,
        Decoration
    }
}
