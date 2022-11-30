using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly Stack<(int, JsonLevel[])> undoStack = new();
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
        /// Determine whether a particular tile in a level is free to have a wall placed on it.
        /// </summary>
        private bool IsTileFree(System.Drawing.Point tile, Level? lvl = null)
        {
            lvl ??= levels[currentLevel];
            if (!lvl.IsCoordInBounds(tile))
            {
                return false;
            }
            if (tile == lvl.StartPoint || lvl.EndPoint == lvl.StartPoint || lvl.MonsterStart == lvl.StartPoint)
            {
                return false;
            }
            if (lvl.OriginalExitKeys.Contains(tile) || lvl.OriginalKeySensors.Contains(tile)
                || lvl.OriginalGuns.Contains(tile) || lvl.Decorations.ContainsKey(tile))
            {
                return false;
            }
            return true;
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
                    System.Drawing.Point newOffset = scrollOffset;
                    if (mousePos.X >= mapCanvas.ActualWidth * 0.75)
                    {
                        newOffset = new System.Drawing.Point(newOffset.X + 1, newOffset.Y);
                    }
                    else if (mousePos.X <= mapCanvas.ActualWidth * 0.25)
                    {
                        newOffset = new System.Drawing.Point(newOffset.X - 1, newOffset.Y);
                    }
                    if (mousePos.Y >= mapCanvas.ActualHeight * 0.75)
                    {
                        newOffset = new System.Drawing.Point(newOffset.X, newOffset.Y + 1);
                    }
                    else if (mousePos.Y <= mapCanvas.ActualHeight * 0.25)
                    {
                        newOffset = new System.Drawing.Point(newOffset.X, newOffset.Y - 1);
                    }
                    // If we can't move diagonally, try moving in just one dimension instead.
                    foreach (System.Drawing.Point tryOffset in new System.Drawing.Point[] {
                        newOffset, new System.Drawing.Point(newOffset.X, scrollOffset.Y), new System.Drawing.Point(scrollOffset.X, newOffset.Y) })
                    {
                        if (lvl.IsCoordInBounds(Math.Max(1, (int)(lvl.Dimensions.Width * zoomLevel) + tryOffset.X - 1),
                            Math.Max(1, (int)(lvl.Dimensions.Height * zoomLevel) + tryOffset.Y - 1)) && lvl.IsCoordInBounds(tryOffset))
                        {
                            // New scroll offset remains in level boundaries
                            scrollOffset = tryOffset;
                            UpdateMapCanvas();
                            break;
                        }
                    }
                    break;
                case Tool.Wall:
                    if (!IsTileFree(clickedTile))
                    {
                        return;
                    }
                    AddToUndo();
                    Level.GridSquareContents gridSquare = lvl[clickedTile];
                    Level.GridSquareContents newGridSquare = new(gridSquare.Wall is not null ? null
                        : (lvl.EdgeWallTextureName, lvl.EdgeWallTextureName, lvl.EdgeWallTextureName, lvl.EdgeWallTextureName),
                        gridSquare.Wall is null, gridSquare.Wall is null);
                    lvl[clickedTile] = newGridSquare;
                    break;
                case Tool.CollisionPlayer:
                    if (clickedTile != lvl.MonsterStart && !IsTileFree(clickedTile))
                    {
                        return;
                    }
                    AddToUndo();
                    gridSquare = lvl[clickedTile];
                    newGridSquare = new(gridSquare.Wall, !gridSquare.PlayerCollide, gridSquare.MonsterCollide);
                    lvl[clickedTile] = newGridSquare;
                    break;
                case Tool.CollisionMonster:
                    if (clickedTile == lvl.MonsterStart)
                    {
                        return;
                    }
                    AddToUndo();
                    gridSquare = lvl[clickedTile];
                    newGridSquare = new(gridSquare.Wall, gridSquare.PlayerCollide, !gridSquare.MonsterCollide);
                    lvl[clickedTile] = newGridSquare;
                    break;
                case Tool.Start:
                    if (lvl[clickedTile].Wall is not null || lvl[clickedTile].PlayerCollide || !IsTileFree(clickedTile))
                    {
                        return;
                    }
                    AddToUndo();
                    lvl.StartPoint = clickedTile;
                    break;
                case Tool.End:
                    if (lvl[clickedTile].Wall is not null || lvl[clickedTile].PlayerCollide || !IsTileFree(clickedTile))
                    {
                        return;
                    }
                    AddToUndo();
                    lvl.EndPoint = clickedTile;
                    break;
                case Tool.Key:
                    if (lvl.OriginalExitKeys.Contains(clickedTile))
                    {
                        AddToUndo();
                        lvl.OriginalExitKeys = lvl.OriginalExitKeys.Remove(clickedTile);
                    }
                    else
                    {
                        if (lvl[clickedTile].Wall is not null || lvl[clickedTile].PlayerCollide || !IsTileFree(clickedTile))
                        {
                            return;
                        }
                        AddToUndo();
                        lvl.OriginalExitKeys = lvl.OriginalExitKeys.Add(clickedTile);
                    }
                    break;
                case Tool.Sensor:
                    if (lvl.OriginalKeySensors.Contains(clickedTile))
                    {
                        AddToUndo();
                        lvl.OriginalKeySensors = lvl.OriginalKeySensors.Remove(clickedTile);
                    }
                    else
                    {
                        if (lvl[clickedTile].Wall is not null || lvl[clickedTile].PlayerCollide || !IsTileFree(clickedTile))
                        {
                            return;
                        }
                        AddToUndo();
                        lvl.OriginalKeySensors = lvl.OriginalKeySensors.Add(clickedTile);
                    }
                    break;
                case Tool.Gun:
                    if (lvl.OriginalGuns.Contains(clickedTile))
                    {
                        AddToUndo();
                        lvl.OriginalGuns = lvl.OriginalGuns.Remove(clickedTile);
                    }
                    else
                    {
                        if (lvl[clickedTile].Wall is not null || lvl[clickedTile].PlayerCollide || !IsTileFree(clickedTile))
                        {
                            return;
                        }
                        AddToUndo();
                        lvl.OriginalGuns = lvl.OriginalGuns.Add(clickedTile);
                    }
                    break;
                case Tool.Monster:
                    if (clickedTile == lvl.MonsterStart)
                    {
                        AddToUndo();
                        lvl.MonsterStart = null;
                        lvl.MonsterWait = null;
                    }
                    else
                    {
                        if (lvl[clickedTile].Wall is not null || lvl[clickedTile].MonsterCollide || !IsTileFree(clickedTile))
                        {
                            return;
                        }
                        AddToUndo();
                        lvl.MonsterStart = clickedTile;
                        lvl.MonsterWait ??= 10;
                    }
                    break;
                case Tool.Decoration:
                    if (lvl.Decorations.ContainsKey(clickedTile))
                    {
                        AddToUndo();
                        _ = lvl.Decorations.Remove(clickedTile);
                    }
                    else
                    {
                        if (lvl[clickedTile].Wall is not null || !IsTileFree(clickedTile))
                        {
                            return;
                        }
                        AddToUndo();
                        lvl.Decorations = lvl.Decorations.Add(clickedTile, (string)((ComboBoxItem)decorationTextureDropdown.Items[0]).Content);
                    }
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
            undoStack.Push((currentLevel, levels.Select(x => (JsonLevel)x).ToArray()));
            undoButton.IsEnabled = true;
        }

        /// <summary>
        /// Revert the current level to its state before the most recent non-undone action.
        /// </summary>
        private void PerformUndo()
        {
            if (undoStack.Count > 0)
            {
                (int oldLevel, JsonLevel[] jsonLevels) = undoStack.Pop();
                levels = jsonLevels.Select(x => (Level)x).ToArray();
                UpdateLevelList();
                levelSelect.SelectedIndex = oldLevel;
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
            if (currentLevel < 0 || !doUpdates)
            {
                return;
            }
            System.Drawing.Size newDimensions = new((int)Math.Round(widthDimensionSlider.Value), (int)Math.Round(heightDimensionSlider.Value));
            Level lvl = levels[currentLevel];
            if (newDimensions == lvl.Dimensions)
            {
                return;
            }
            AddToUndo();
            bulkWallSelection.Clear();
            System.Drawing.Size oldDimensions = lvl.Dimensions;
            lvl.Dimensions = newDimensions;
            if (!lvl.IsCoordInBounds(lvl.StartPoint) || !lvl.IsCoordInBounds(lvl.EndPoint))
            {
                // Don't allow the user to shrink start/end points out of bounds.
                lvl.Dimensions = new System.Drawing.Size(Math.Max(lvl.StartPoint.X + 1, Math.Max(lvl.EndPoint.X + 1, lvl.Dimensions.Width)),
                    Math.Max(lvl.StartPoint.Y + 1, Math.Max(lvl.EndPoint.Y + 1, lvl.Dimensions.Height)));
            }
            System.Drawing.Point? monsterStart = lvl.MonsterStart;
            if (monsterStart is not null && !lvl.IsCoordInBounds(monsterStart.Value))
            {
                lvl.Dimensions = new System.Drawing.Size(Math.Max(monsterStart.Value.X + 1, lvl.Dimensions.Width),
                    Math.Max(monsterStart.Value.Y + 1, lvl.Dimensions.Height));
            }
            if (oldDimensions == lvl.Dimensions)
            {
                PerformUndo();
                return;
            }
            // Remove out of bounds keys, sensors, and guns.
            lvl.OriginalExitKeys = lvl.OriginalExitKeys.Where(x => lvl.IsCoordInBounds(x)).ToImmutableHashSet();
            lvl.OriginalKeySensors = lvl.OriginalKeySensors.Where(x => lvl.IsCoordInBounds(x)).ToImmutableHashSet();
            lvl.OriginalGuns = lvl.OriginalGuns.Where(x => lvl.IsCoordInBounds(x)).ToImmutableHashSet();
            // Remove excess rows and columns
            (string, string, string, string)?[,] newWallMap = new (string, string, string, string)?[lvl.Dimensions.Width, lvl.Dimensions.Height];
            for (int y = 0; y < Math.Min(lvl.Dimensions.Height, lvl.WallMap.GetLength(1)); y++)
            {
                for (int x = 0; x < Math.Min(lvl.Dimensions.Width, lvl.WallMap.GetLength(0)); x++)
                {
                    newWallMap[x, y] = lvl.WallMap[x, y];
                }
            }
            lvl.WallMap = newWallMap;
            (bool, bool)[,] newCollisionMap = new (bool, bool)[lvl.Dimensions.Width, lvl.Dimensions.Height];
            for (int y = 0; y < Math.Min(lvl.Dimensions.Height, lvl.CollisionMap.GetLength(1)); y++)
            {
                for (int x = 0; x < Math.Min(lvl.Dimensions.Width, lvl.CollisionMap.GetLength(0)); x++)
                {
                    newCollisionMap[x, y] = lvl.CollisionMap[x, y];
                }
            }
            lvl.CollisionMap = newCollisionMap;
            if (!lvl.IsCoordInBounds(currentTile))
            {
                currentTile = new System.Drawing.Point(-1, -1);
            }
            zoomLevel = 1;
            scrollOffset = new System.Drawing.Point(0, 0);
            doUpdates = false;
            zoomSlider.Value = 1;
            doUpdates = true;
            UpdatePropertiesPanel();
            UpdateLevelList();
            UpdateMapCanvas();
        }

        private void monsterWaitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (currentLevel < 0 || !doUpdates)
            {
                return;
            }
            int roundedTime = (int)Math.Round(monsterWaitSlider.Value) * 5;
            Level lvl = levels[currentLevel];
            if (roundedTime == lvl.MonsterWait)
            {
                return;
            }
            AddToUndo();
            lvl.MonsterWait = roundedTime;
            UpdatePropertiesPanel();
        }

        private void textureDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentLevel < 0 || !doUpdates || !levels[currentLevel].IsCoordInBounds(currentTile))
            {
                return;
            }
            AddToUndo();
            Level lvl = levels[currentLevel];
            foreach (System.Drawing.Point tile in bulkWallSelection)
            {
                Level.GridSquareContents gridSquare = lvl[tile];
                if (gridSquare.Wall is not null)
                {
                    string[] textures = new string[4] { gridSquare.Wall.Value.Item1, gridSquare.Wall.Value.Item2,
                        gridSquare.Wall.Value.Item3, gridSquare.Wall.Value.Item4 };
                    textures[(int)SelectedDirection] = (string)((ComboBoxItem)e.AddedItems[0]!).Content;
                    lvl[tile] = new Level.GridSquareContents((textures[0], textures[1], textures[2], textures[3]),
                        gridSquare.PlayerCollide, gridSquare.MonsterCollide);
                }
            }
            UpdatePropertiesPanel();
        }

        private void edgeTextureDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentLevel < 0 || !doUpdates)
            {
                return;
            }
            AddToUndo();
            levels[currentLevel].EdgeWallTextureName = (string)((ComboBoxItem)e.AddedItems[0]!).Content;
            UpdatePropertiesPanel();
        }

        private void decorationTextureDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentLevel < 0 || !doUpdates || !levels[currentLevel].IsCoordInBounds(currentTile))
            {
                return;
            }
            AddToUndo();
            levels[currentLevel].Decorations = levels[currentLevel].Decorations.SetItem(currentTile,
                (string)((ComboBoxItem)e.AddedItems[0]!).Content);
            UpdatePropertiesPanel();
        }

        private void undoButton_Click(object sender, RoutedEventArgs e)
        {
            PerformUndo();
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
            AddToUndo();
            List<Level> levelList = levels.ToList();
            levelList.Insert(currentLevel + 1, new Level(new System.Drawing.Size(10, 10), (string)((ComboBoxItem)edgeTextureDropdown.Items[0]).Content,
                new (string, string, string, string)?[10, 10], new (bool, bool)[10, 10], new System.Drawing.Point(0, 0),
                new System.Drawing.Point(1, 0), new HashSet<System.Drawing.Point>(), new HashSet<System.Drawing.Point>(),
                new HashSet<System.Drawing.Point>(), new Dictionary<System.Drawing.Point, string>(), null, null));
            levels = levelList.ToArray();
            UpdateLevelList();
            UpdateMapCanvas();
            UpdatePropertiesPanel();
        }

        private void levelDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentLevel < 0)
            {
                return;
            }
            if (MessageBox.Show("Are you sure you want to delete this level? While it may be temporarily possible to undo, " +
                "it should not be depended upon!", "Delete level", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }
            AddToUndo();
            List<Level> levelList = levels.ToList();
            levelList.RemoveAt(currentLevel);
            levels = levelList.ToArray();
            currentLevel = -1;
            UpdateLevelList();
            UpdateMapCanvas();
            UpdatePropertiesPanel();
        }

        private void levelMoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentLevel <= 0)
            {
                return;
            }
            AddToUndo();
            (levels[currentLevel], levels[currentLevel - 1]) = (levels[currentLevel - 1], levels[currentLevel]);
            currentLevel--;
            UpdateLevelList();
        }

        private void levelMoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentLevel < 0 || currentLevel >= levels.Length - 1)
            {
                return;
            }
            AddToUndo();
            (levels[currentLevel], levels[currentLevel + 1]) = (levels[currentLevel + 1], levels[currentLevel]);
            currentLevel++;
            UpdateLevelList();
        }

        private void levelSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!doUpdates)
            {
                return;
            }
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = unsavedChanges && MessageBox.Show("You currently have unsaved changes, are you sure you wish to exit?",
                "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No;
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
