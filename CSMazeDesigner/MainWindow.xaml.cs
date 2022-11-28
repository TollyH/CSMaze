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
        private readonly Stack<(int, IEnumerable<JsonLevel>)> undoStack = new();
        private bool unsavedChanges = false;
        // Used to prevent methods from being called when programmatically setting widget values.
        private bool doUpdates = true;

        private readonly Dictionary<Tool, string> descriptions = new();
        private readonly Dictionary<Tool, Button> toolButtons = new();

        private WallDirection SelectedDirection => textureDimensionWest.IsChecked!.Value ? WallDirection.West : textureDimensionEast.IsChecked!.Value
            ? WallDirection.East : textureDimensionSouth.IsChecked!.Value ? WallDirection.South : WallDirection.North;

        public MainWindow() : this(null) { }

        public MainWindow(string? configIniPath = "config.ini")
        {
            // Change working directory to the directory where the script is located.
            // This prevents issues with required files not being found.
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            cfg = new Config(configIniPath ?? "config.ini");
            InitializeComponent();
            foreach (Button btn in toolButtonPanel.Children.OfType<Button>())
            {
                toolButtons[(Tool)btn.Tag] = btn;
            }
            mapCanvas.Width = cfg.ViewportWidth + 50;
            mapCanvas.Height = cfg.ViewportHeight + 50;
            string[] descLines = File.ReadAllLines("level_designer_descriptions.txt");
            foreach (string descLine in descLines)
            {
                string[] split = descLine.Split('|');
                descriptions[Enum.Parse<Tool>(split[0])] = split[1];
            }
            foreach (string filepath in Directory.EnumerateFiles(System.IO.Path.Join("textures", "wall"), "*.png"))
            {
                _ = textureDropdown.Items.Add(new ComboBoxItem() { Content = filepath.Split("\\")[^1].Split(".")[0] });
                _ = edgeTextureDropdown.Items.Add(new ComboBoxItem() { Content = filepath.Split("\\")[^1].Split(".")[0] });
            }
            foreach (string filepath in Directory.EnumerateFiles(System.IO.Path.Join("textures", "sprite", "decoration"), "*.png"))
            {
                _ = decorationTextureDropdown.Items.Add(new ComboBoxItem() { Content = filepath.Split("\\")[^1].Split(".")[0] });
            }
            texturePreview.Width = MazeGame.TextureWidth;
            texturePreview.Height = MazeGame.TextureHeight;
            edgeTexturePreview.Width = MazeGame.TextureWidth;
            edgeTexturePreview.Height = MazeGame.TextureHeight;
            decorationTexturePreview.Width = MazeGame.TextureWidth;
            decorationTexturePreview.Height = MazeGame.TextureHeight;
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
                UpdatePropertiesPanel();
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

        /// <summary>
        /// Update the properties panel with information about the selected tile.
        /// Also updates the width and height sliders.
        /// </summary>
        private void UpdatePropertiesPanel()
        {
            if (!doUpdates)
            {
                return;
            }
            if (currentLevel < 0)
            {
                selectedSquareDescription.Background = null;
                selectedSquareDescription.Foreground = Brushes.Black;
                selectedSquareDescription.Text = "Nothing is currently selected";
                dimensionsPanel.Visibility = Visibility.Collapsed;
                monsterWaitPanel.Visibility = Visibility.Collapsed;
                texturesPanel.Visibility = Visibility.Collapsed;
                edgeTexturesPanel.Visibility = Visibility.Collapsed;
                decorationTexturesPanel.Visibility = Visibility.Collapsed;
                return;
            }
            dimensionsPanel.Visibility = Visibility.Visible;
            Level lvl = levels[currentLevel];
            doUpdates = false;
            widthDimensionLabel.Content = $"Level width - ({lvl.Dimensions.Width})";
            widthDimensionSlider.Value = lvl.Dimensions.Width;
            heightDimensionLabel.Content = $"Level height - ({lvl.Dimensions.Height})";
            heightDimensionSlider.Value = lvl.Dimensions.Height;
            doUpdates = true;
            // Remove all property widgets that apply to only a certain type of grid square.
            monsterWaitPanel.Visibility = Visibility.Collapsed;
            texturesPanel.Visibility = Visibility.Collapsed;
            edgeTexturesPanel.Visibility = Visibility.Collapsed;
            decorationTexturesPanel.Visibility = Visibility.Collapsed;
            if (currentTile.X == -1 || currentTile.Y == -1)
            {
                selectedSquareDescription.Background = null;
                selectedSquareDescription.Foreground = Brushes.Black;
                selectedSquareDescription.Text = "Nothing is currently selected";
            }
            else if (lvl.OriginalExitKeys.Contains(currentTile))
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Gold.R, ScreenDrawing.Gold.G, ScreenDrawing.Gold.B));
                selectedSquareDescription.Foreground = Brushes.Black;
                selectedSquareDescription.Text = descriptions[Tool.Key];
            }
            else if (lvl.OriginalKeySensors.Contains(currentTile))
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.DarkGold.R, ScreenDrawing.DarkGold.G, ScreenDrawing.DarkGold.B));
                selectedSquareDescription.Foreground = Brushes.White;
                selectedSquareDescription.Text = descriptions[Tool.Sensor];
            }
            else if (lvl.OriginalGuns.Contains(currentTile))
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Grey.R, ScreenDrawing.Grey.G, ScreenDrawing.Grey.B));
                selectedSquareDescription.Foreground = Brushes.Black;
                selectedSquareDescription.Text = descriptions[Tool.Gun];
            }
            else if (lvl.Decorations.ContainsKey(currentTile))
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Purple.R, ScreenDrawing.Purple.G, ScreenDrawing.Purple.B));
                selectedSquareDescription.Foreground = Brushes.White;
                selectedSquareDescription.Text = descriptions[Tool.Decoration];
                decorationTexturesPanel.Visibility = Visibility.Visible;
                doUpdates = false;
                decorationTextureDropdown.Text = lvl.Decorations[currentTile];
                doUpdates = true;
                decorationTexturePreview.Source = new BitmapImage(
                    new Uri(System.IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory, "textures", "sprite", "decoration", lvl.Decorations[currentTile]) + ".png"));
            }
            else if (lvl.MonsterStart == currentTile)
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.DarkRed.R, ScreenDrawing.DarkRed.G, ScreenDrawing.DarkRed.B));
                selectedSquareDescription.Foreground = Brushes.White;
                selectedSquareDescription.Text = descriptions[Tool.Monster];
                monsterWaitPanel.Visibility = Visibility.Visible;
                if (lvl.MonsterWait is not null)
                {
                    doUpdates = false;
                    monsterWaitLabel.Content = $"Monster spawn time - ({Math.Round(lvl.MonsterWait.Value)})";
                    monsterWaitSlider.Value = lvl.MonsterWait.Value / 5;
                    doUpdates = true;
                }
            }
            else if (lvl.StartPoint == currentTile)
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Red.R, ScreenDrawing.Red.G, ScreenDrawing.Red.B));
                selectedSquareDescription.Foreground = Brushes.White;
                selectedSquareDescription.Text = descriptions[Tool.Start];
            }
            else if (lvl.EndPoint == currentTile)
            {
                selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Green.R, ScreenDrawing.Green.G, ScreenDrawing.Green.B));
                selectedSquareDescription.Foreground = Brushes.Black;
                selectedSquareDescription.Text = descriptions[Tool.End];
            }
            else
            {
                if (lvl[currentTile].Wall is null)
                {
                    selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.White.R, ScreenDrawing.White.G, ScreenDrawing.White.B));
                    selectedSquareDescription.Foreground = Brushes.Black;
                    selectedSquareDescription.Text = descriptions[Tool.Select];
                    edgeTexturesPanel.Visibility = Visibility.Visible;
                    doUpdates = false;
                    edgeTextureDropdown.Text = lvl.EdgeWallTextureName;
                    doUpdates = true;
                    edgeTexturePreview.Source = new BitmapImage(
                        new Uri(System.IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory, "textures", "wall", lvl.EdgeWallTextureName) + ".png"));
                }
                else
                {
                    selectedSquareDescription.Background = new SolidColorBrush(Color.FromRgb(ScreenDrawing.Black.R, ScreenDrawing.Black.G, ScreenDrawing.Black.B));
                    selectedSquareDescription.Foreground = Brushes.White;
                    selectedSquareDescription.Text = descriptions[Tool.Wall];
                    texturesPanel.Visibility = Visibility.Visible;
                    textureDimensionNorth.IsChecked = true;
                    (string, string, string, string)? tileTextures = lvl[currentTile].Wall;
                    if (tileTextures is not null)
                    {
                        string texture = SelectedDirection == WallDirection.North ? tileTextures.Value.Item1 : SelectedDirection == WallDirection.East
                            ? tileTextures.Value.Item2 : SelectedDirection == WallDirection.South ? tileTextures.Value.Item3 : tileTextures.Value.Item4;
                        doUpdates = false;
                        textureDropdown.Text = texture;
                        doUpdates = true;
                        texturePreview.Source = new BitmapImage(
                            new Uri(System.IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory, "textures", "wall", texture) + ".png"));
                    }
                }
            }
        }

        /// <summary>
        /// Called when the map canvas is clicked by the user or the mouse is moved while the left mouse button is held down.
        /// Handles the event based on the currently selected tool.
        /// </summary>
        /// <param name="e">The event args from the original mouse event</param>
        private void CanvasMouseEvent(MouseEventArgs e, bool wasClick)
        {
            if (currentLevel < 0)
            {
                return;
            }
            Level lvl = levels[currentLevel];
            int tileWidth = (int)mapCanvas.ActualWidth / (int)Math.Max(lvl.Dimensions.Width * zoomLevel, 1);
            int tileHeight = (int)mapCanvas.ActualHeight / (int)Math.Max(lvl.Dimensions.Height * zoomLevel, 1);
            Point mousePos = e.GetPosition(mapCanvas);
            System.Drawing.Point clickedTile = new((int)(mousePos.X / (tileWidth - 1)) + scrollOffset.X, (int)(mousePos.Y / (tileHeight - 1)) + scrollOffset.Y);
            if (!lvl.IsCoordInBounds(clickedTile))
            {
                return;
            }
            if (!wasClick && clickedTile == lastVisitedTile)
            {
                return;
            }
            lastVisitedTile = clickedTile;
            switch (currentTool)
            {
                case Tool.Select:
                    currentTile = clickedTile;
                    if (lvl[clickedTile].Wall is not null)
                    {
                        bulkWallSelection.Add(currentTile);
                    }
                    else if (wasClick)
                    {
                        bulkWallSelection.Clear();
                    }
                    break;
                case Tool.Move:
                    break;
                case Tool.Wall:
                    break;
                case Tool.CollisionPlayer:
                    break;
                case Tool.CollisionMonster:
                    break;
                case Tool.Start:
                    break;
                case Tool.End:
                    break;
                case Tool.Key:
                    break;
                case Tool.Sensor:
                    break;
                case Tool.Gun:
                    break;
                case Tool.Monster:
                    break;
                case Tool.Decoration:
                    break;
                default: break;
            }
            UpdateMapCanvas();
            UpdatePropertiesPanel();
        }

        /// <summary>
        /// Add the state of all the current levels to the undo stack.
        /// </summary>
        /// <remarks>Also marks the file as having unsaved changes.</remarks>
        private void AddToUndo()
        {
            unsavedChanges = true;
            undoStack.Push((currentLevel, levels.Cast<JsonLevel>()));
            undoButton.IsEnabled = true;
        }

        /// <summary>
        /// Revert the current level to its state before the most recent non-undone action.
        /// </summary>
        private void PerformUndo()
        {
            if (undoStack.Count > 0)
            {
                (currentLevel, IEnumerable<JsonLevel> jsonLevels) = undoStack.Pop();
                levels = jsonLevels.Cast<Level>().ToArray();
                UpdateLevelList();
                UpdateMapCanvas();
                UpdatePropertiesPanel();
            }
            if (undoStack.Count == 0)
            {
                undoButton.IsEnabled = false;
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
            else if (e.Key == Key.W)
            {
                SelectTool(currentTool - 1);
            }
            else if (e.Key == Key.A)
            {
                if (currentLevel < 0 || currentTile.X == -1 || currentTile.Y == -1)
                {
                    return;
                }
                if (levels[currentLevel][currentTile].Wall is null)
                {
                    return;
                }
                for (int y = 0; y < levels[currentLevel].Dimensions.Height; y++)
                {
                    for (int x = 0; x < levels[currentLevel].Dimensions.Width; x++)
                    {
                        if (levels[currentLevel][x, y].Wall is not null)
                        {
                            bulkWallSelection.Add(new System.Drawing.Point(x, y));
                        }
                    }
                }
                UpdateMapCanvas();
            }
        }

        private void mapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CanvasMouseEvent(e, true);
        }

        private void mapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                CanvasMouseEvent(e, false);
            }
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
                UpdatePropertiesPanel();
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
