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
        private readonly List<System.Drawing.Point> bulkWallSelection = new();
        // The mouse must leave a tile before that same tile is modified again.
        private System.Drawing.Point lastVisitedTile = new(-1, -1);
        private double zoomLevel = 1;
        private System.Drawing.Point scrollOffset = new(0, 0);
        private readonly Stack<(int, Level[])> undoStack = new();
        private bool unsavedChanges = false;
        // Used to prevent methods from being called when programmatically setting widget values.
        private bool doUpdates = true;

        private readonly Dictionary<Tool, string> descriptions = new();
        private readonly Dictionary<Tool, Button> toolButtons = new();
        private readonly Dictionary<string, Image> textures = new();
        private readonly Dictionary<string, Image> decorationTextures = new();

        public MainWindow() : this(null) { }

        public MainWindow(string? configIniPath = "config.ini")
        {
            cfg = new Config(configIniPath ?? "config.ini");
            InitializeComponent();
            foreach (Button btn in toolButtonPanel.Children.OfType<Button>())
            {
                toolButtons[(Tool)btn.Tag] = btn;
            }
            mapCanvas.Width = cfg.ViewportWidth + 50;
            mapCanvas.Height = cfg.ViewportHeight + 50;
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
                Filter = "JSON files|*.json",
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
                UpdateLevelList();
                UpdateMapCanvas();
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
                    Filter = "JSON files|*.json",
                    DefaultExt = ".json",
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

        /// <summary>
        /// Draw the current level to the map canvas.
        /// </summary>
        private void UpdateMapCanvas()
        {
            if (!doUpdates)
            {
                return;
            }
            mapCanvas.Children.Clear();
            if (currentLevel < 0)
            {
                return;
            }
            Level lvl = levels[currentLevel];
            int tileWidth = (int)mapCanvas.ActualWidth / (int)Math.Max(lvl.Dimensions.Width * zoomLevel, 1);
            int tileHeight = (int)mapCanvas.ActualHeight / (int)Math.Max(lvl.Dimensions.Height * zoomLevel, 1);
            List<(int, int, SolidColorBrush, SolidColorBrush)> tilesToRedraw = new();
            for (int y = 0; y < lvl.Dimensions.Height - scrollOffset.Y; y++)
            {
                for (int x = 0; x < lvl.Dimensions.Width - scrollOffset.X ; x++)
                {
                    System.Drawing.Point tileCoord = new(x + scrollOffset.X, y + scrollOffset.Y);
                    System.Drawing.Color colour;
                    if (lvl.OriginalExitKeys.Contains(tileCoord))
                    {
                        colour = ScreenDrawing.Gold;
                    }
                    else if (lvl.OriginalKeySensors.Contains(tileCoord))
                    {
                        colour = ScreenDrawing.DarkGold;
                    }
                    else if (lvl.OriginalGuns.Contains(tileCoord))
                    {
                        colour = ScreenDrawing.Grey;
                    }
                    else if (lvl.Decorations.ContainsKey(tileCoord))
                    {
                        colour = ScreenDrawing.Purple;
                    }
                    else if (lvl.MonsterStart == tileCoord)
                    {
                        colour = ScreenDrawing.DarkRed;
                    }
                    else if (lvl.StartPoint == tileCoord)
                    {
                        colour = ScreenDrawing.Red;
                    }
                    else if (lvl.EndPoint == tileCoord)
                    {
                        colour = ScreenDrawing.Green;
                    }
                    else
                    {
                        colour = lvl[tileCoord].Wall is null ? ScreenDrawing.White : ScreenDrawing.Black;
                    }
                    SolidColorBrush newBrush = new(Color.FromRgb(colour.R, colour.G, colour.B));
                    if (currentTile == tileCoord)
                    {
                        tilesToRedraw.Add((x, y, newBrush, new SolidColorBrush(Color.FromRgb(ScreenDrawing.Red.R, ScreenDrawing.Red.G, ScreenDrawing.Red.B))));
                    }
                    else if (bulkWallSelection.Contains(tileCoord))
                    {
                        tilesToRedraw.Add((x, y, newBrush, new SolidColorBrush(Color.FromRgb(ScreenDrawing.Green.R, ScreenDrawing.Green.G, ScreenDrawing.Green.B))));
                    }
                    else
                    {
                        Rectangle newRect = new()
                        {
                            Width = tileWidth,
                            Height = tileHeight,
                            Fill = new SolidColorBrush(Color.FromRgb(colour.R, colour.G, colour.B)),
                            Stroke = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Black.R, ScreenDrawing.Black.G, ScreenDrawing.Black.B)),
                            StrokeThickness = 1
                        };
                        Canvas.SetLeft(newRect, (tileWidth - 1) * x);
                        Canvas.SetTop(newRect, (tileHeight - 1) * y);
                        _ = mapCanvas.Children.Add(newRect);
                    }
                }
            }
            // Redraw the selected tile(s) to keep the entire outline on top.
            foreach ((int, int, SolidColorBrush, SolidColorBrush) tile in tilesToRedraw)
            {
                Rectangle newRect = new()
                {
                    Width = tileWidth,
                    Height = tileHeight,
                    Fill = tile.Item3,
                    Stroke = tile.Item4,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(newRect, (tileWidth - 1) * tile.Item1);
                Canvas.SetTop(newRect, (tileHeight - 1) * tile.Item2);
                _ = mapCanvas.Children.Add(newRect);
            }
            for (int y = 0; y < lvl.Dimensions.Height - scrollOffset.Y; y++)
            {
                for (int x = 0; x < lvl.Dimensions.Width - scrollOffset.X; x++)
                {
                    System.Drawing.Point tileCoord = new(x + scrollOffset.X, y + scrollOffset.Y);
                    Level.GridSquareContents gridSquare = lvl[tileCoord];
                    if (gridSquare.PlayerCollide)
                    {
                        Ellipse newEllipse = new()
                        {
                            Width = tileWidth / 8d,
                            Height = tileHeight / 8d,
                            Fill = new SolidColorBrush(Color.FromRgb(ScreenDrawing.DarkGreen.R, ScreenDrawing.DarkGreen.G, ScreenDrawing.DarkGreen.B)),
                            Stroke = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Black.R, ScreenDrawing.Black.G, ScreenDrawing.Black.B)),
                            StrokeThickness = 1
                        };
                        Canvas.SetLeft(newEllipse, ((tileWidth - 1) * x) + 3);
                        Canvas.SetTop(newEllipse, ((tileHeight - 1) * y) + (tileHeight - (tileHeight / 8)) - 3);
                        _ = mapCanvas.Children.Add(newEllipse);
                    }
                    if (gridSquare.MonsterCollide)
                    {
                        Ellipse newEllipse = new()
                        {
                            Width = tileWidth / 8d,
                            Height = tileHeight / 8d,
                            Fill = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Red.R, ScreenDrawing.Red.G, ScreenDrawing.Red.B)),
                            Stroke = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Black.R, ScreenDrawing.Black.G, ScreenDrawing.Black.B)),
                            StrokeThickness = 1
                        };
                        Canvas.SetLeft(newEllipse, ((tileWidth - 1) * x) + (tileWidth - (tileWidth / 8)) - 3);
                        Canvas.SetTop(newEllipse, ((tileHeight - 1) * y) + (tileHeight - (tileHeight / 8)) - 3);
                        _ = mapCanvas.Children.Add(newEllipse);
                    }
                }
            }
        }

        /// <summary>
        /// Update level list with the current state of all the levels.
        /// </summary>
        private void UpdateLevelList()
        {
            if (!doUpdates)
            {
                return;
            }
            doUpdates = false;
            levelSelect.Items.Clear();
            for (int i = 0; i < levels.Length; i++)
            {
                _ = levelSelect.Items.Add(new ListBoxItem()
                {
                    Content = $"Level {i + 1} - {levels[i].Dimensions.Width}x{levels[i].Dimensions.Height}"
                });
            }
            if (0 <= currentLevel && currentLevel < levels.Length)
            {
                levelSelect.SelectedIndex = currentLevel;
            }
            doUpdates = true;
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
            if (currentLevel < 0 || !doUpdates)
            {
                return;
            }
            zoomLevel = zoomSlider.Value;
            Level lvl = levels[currentLevel];
            if (!lvl.IsCoordInBounds((int)Math.Max(lvl.Dimensions.Width * zoomLevel, 1) + scrollOffset.X - 1,
                (int)Math.Max(lvl.Dimensions.Height * zoomLevel, 1) + scrollOffset.Y - 1))
            {
                // Zoomed out enough to have current offset go over level boundary, so reset offset.
                scrollOffset = new System.Drawing.Point(0, 0);
            }
            UpdateMapCanvas();
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

        private void levelSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selection = levelSelect.SelectedIndex >= 0 ? levelSelect.SelectedIndex : -1;
            if (selection != currentLevel)
            {
                currentLevel = selection;
                currentTile = new System.Drawing.Point(-1, -1);
                bulkWallSelection.Clear();
                zoomLevel = 1;
                scrollOffset = new System.Drawing.Point(0, 0);
                doUpdates = false;
                zoomSlider.Value = 1;
                doUpdates = true;
                UpdateMapCanvas();
                // TODO: UpdatePropertiesFrame();
            }
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
