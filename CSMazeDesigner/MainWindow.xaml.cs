using System;
using System.Collections.Generic;
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

        private Dictionary<Tool, string> descriptions;
        private Dictionary<string, Image> textures;
        private Dictionary<string, Image> decorationTextures;

        public MainWindow() : this(null) { }

        public MainWindow(string? configIniPath = "config.ini")
        {
            cfg = new Config(configIniPath ?? "config.ini");
            InitializeComponent();
        }

        private void ToolButton_Click(object sender, RoutedEventArgs e)
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
