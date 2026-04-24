using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IoPath = System.IO.Path;
using Microsoft.Win32;

namespace MagicalChalkStudio
{
    public partial class MainWindow : Window
    {
        private enum EditorTool
        {
            Select,
            Line,
            Block1,
            Block2x1,
            Block2x2,
            Brush3,
            Eraser,
            FillRect,
            WallRect,
            FillCircle,
            WallCircle,
            FillEllipse,
            WallEllipse,
            ZoneSelect
        }

        private const double GridSize = StructureJson.GridSize;
        private const double MinZoom = 0.01;
        private const double MaxZoom = 1.0;
        private const double ZoomStep = 1.02;

        private readonly List<PlacedBlock> _blocks = new List<PlacedBlock>();
        private readonly HashSet<PlacedBlock> _selection = new HashSet<PlacedBlock>();
        private readonly List<PlacedBlock> _clipboard = new List<PlacedBlock>();
        private readonly List<string> _paletteAll = new List<string>();
        private readonly EditorHistory _history = new EditorHistory();
        private readonly System.Windows.Threading.DispatcherTimer _gridDebounce;

        private EditorTool _tool = EditorTool.Select;
        private int _currentLayer;
        private Point? _dragStart;
        private bool _suppressZoom;
        /// <summary>Limites de la scène en unités monde (1 bloc = <see cref="StructureJson.GridSize"/> unités). Les champs « Taille » expriment le nombre de blocs.</summary>
        private double _sceneWidth = 100 * GridSize;
        private double _sceneHeight = 100 * GridSize;

        /// <summary>Texture layer_hint (JavaFX) — <c>layer_hint.png</c> / <c>stack_marker.png</c> près de l’exe, ou générée.</summary>
        private ImageSource? _layerHintImageSource;
        private static readonly object LayerHintViewTag = new();
        private static readonly object DirectionOverlayTag = new();
        private bool _suppressColorEvent;
        private bool _suppressLangCombo;
        private (string name, string hex)[]? _colorPresets;

        public MainWindow()
        {
            InitializeComponent();
            _gridDebounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            _gridDebounce.Tick += (_, __) => { _gridDebounce.Stop(); RedrawViewportGrid(); };
            Loaded += OnLoaded;
            SizeChanged += (_, __) => ScheduleGridRedraw();
            Localization.LanguageChanged += OnLocalizationLanguageChanged;
            Closed += (_, __) => Localization.LanguageChanged -= OnLocalizationLanguageChanged;
        }

        private void OnLocalizationLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => ApplyLanguageUi(syncLangCombo: true)));
        }

        private void SyncLangComboSelection()
        {
            if (LangCombo == null) return;
            _suppressLangCombo = true;
            try
            {
                foreach (ComboBoxItem it in LangCombo.Items)
                {
                    if (it.Tag is string t && t.Equals(Localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        LangCombo.SelectedItem = it;
                        return;
                    }
                }
            }
            finally { _suppressLangCombo = false; }
        }

        private void ApplyLanguageUi(bool syncLangCombo = false)
        {
            if (syncLangCombo) SyncLangComboSelection();
            Localization.ApplyLocTags(this);
            Title = Localization.T("WindowTitle");
            UpdateLayerLabel();
            UpdateZoomLabel();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLangCombo || LangCombo?.SelectedItem is not ComboBoxItem item || item.Tag is not string code) return;
            Localization.SetLanguage(code);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Taille (blocs) L×H : chaque case = GridSize unités. Sans « Appliquer », on prend les valeurs XAML.
            if (TryGetSceneSizeFromTextBoxes(out double worldW, out double worldH, out int cellW, out int cellH, out string? err))
            {
                _sceneWidth = worldW;
                _sceneHeight = worldH;
                WorldCanvas.Width = _sceneWidth;
                WorldCanvas.Height = _sceneHeight;
                GridLinesCanvas.Width = _sceneWidth;
                GridLinesCanvas.Height = _sceneHeight;
                SceneWidthBox.Text = cellW.ToString("0", CultureInfo.InvariantCulture);
                SceneHeightBox.Text = cellH.ToString("0", CultureInfo.InvariantCulture);
            }
            else if (err != null)
            {
                SetStatus(err);
            }
            SyncPreviewSizes();
            InitColorPresets();
            LoadPalette();
            SyncLangComboSelection();
            ApplyLanguageUi(false);
            SetStatus(Localization.T("Msg_Ready"));
            _history.SeedInitial(_blocks, _currentLayer);
            ScheduleGridRedraw();
        }

        /// <summary>Champs « Taille (blocs) L×H » = nombre de cases, comme <c>sizeX</c> / <c>sizeZ</c> dans le JSON (× <see cref="StructureJson.GridSize"/> en unités internes).</summary>
        private bool TryGetSceneSizeFromTextBoxes(out double worldW, out double worldH, out int cellW, out int cellH, out string? error)
        {
            worldW = worldH = 0;
            cellW = cellH = 0;
            error = null;
            if (SceneWidthBox == null || SceneHeightBox == null) return false;
            if (!double.TryParse(SceneWidthBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double rawW) ||
                !double.TryParse(SceneHeightBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double rawH))
            {
                error = "Taille : saisir un nombre de blocs (ex. 100)";
                return false;
            }
            cellW = Math.Max(1, (int)Math.Round(rawW));
            cellH = Math.Max(1, (int)Math.Round(rawH));
            worldW = cellW * GridSize;
            worldH = cellH * GridSize;
            return true;
        }

        private void InitColorPresets()
        {
            _colorPresets = new (string, string)[]
            {
                ("Pierre / gris clair", "#BFBFBF"), ("Béton / gris", "#7D7D7D"), ("Bois (chêne)", "#9E6E2F"), ("Pierre", "#888888"),
                ("Gazon", "#5A9E3F"), ("Sable", "#D9C98C"), ("Eau", "#4A7A9A"), ("Lave", "#E55A2B"),
                ("Or", "#F0C84A"), ("Diamant", "#75C9E8"), ("Émeraude", "#3FC278"), ("Verre (bleu)", "#A6D0FF"),
                ("Rouge brique", "#A12E2E"), ("Laine blanche", "#E9E9E9"), ("Obsidienne", "#1E0F3A")
            };
            if (ColorPresetCombo == null) return;
            ColorPresetCombo.Items.Clear();
            foreach (var (n, h) in _colorPresets)
                ColorPresetCombo.Items.Add(n);
            for (int i = 0; i < _colorPresets.Length; i++)
            {
                if (string.Equals(_colorPresets[i].hex, (ColorHexBox.Text ?? "#bfbfbf").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _suppressColorEvent = true;
                    try { ColorPresetCombo.SelectedIndex = i; } finally { _suppressColorEvent = false; }
                    return;
                }
            }
        }

        private void OnColorPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPresetCombo == null || _colorPresets == null || _suppressColorEvent) return;
            if (ColorPresetCombo.SelectedIndex < 0) return;
            if (ColorPresetCombo.SelectedIndex >= _colorPresets.Length) return;
            _suppressColorEvent = true;
            try
            {
                var hex = _colorPresets[ColorPresetCombo.SelectedIndex].hex;
                ColorHexBox.Text = hex;
            }
            finally { _suppressColorEvent = false; }
        }

        private void OnColorHexTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ColorHexBox == null || _suppressColorEvent || _colorPresets == null) return;
            _suppressColorEvent = true;
            try
            {
                string t = (ColorHexBox.Text ?? "").Trim();
                if (!t.StartsWith("#", StringComparison.Ordinal)) t = "#" + t;
                for (int i = 0; i < _colorPresets.Length; i++)
                {
                    if (string.Equals(_colorPresets[i].hex, t, StringComparison.OrdinalIgnoreCase))
                    {
                        ColorPresetCombo.SelectedIndex = i;
                        return;
                    }
                }
                ColorPresetCombo.SelectedIndex = -1;
            }
            finally { _suppressColorEvent = false; }
        }

        private void OnPickColor(object sender, RoutedEventArgs e)
        {
            if (ColorHexBox == null) return;
            using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
            var c = TryParseColor((ColorHexBox.Text ?? "#bfbfbf").Trim());
            dlg.Color = System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var w = System.Windows.Media.Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                ColorHexBox.Text = "#" + w.R.ToString("X2") + w.G.ToString("X2") + w.B.ToString("X2");
            }
        }

        private void SyncPreviewSizes()
        {
            PreviewHost.Width = _sceneWidth;
            PreviewHost.Height = _sceneHeight;
        }

        private int PreviewInsertIndex => WorldCanvas.Children.IndexOf(PreviewHost);

        private void LoadPalette()
        {
            _paletteAll.Clear();
            string path = IoPath.Combine(AppContext.BaseDirectory, "blocks.txt");
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                    _paletteAll.Add(t);
                }
            }
            if (_paletteAll.Count == 0)
                _paletteAll.AddRange(new[] { "minecraft:stone", "minecraft:cobblestone", "minecraft:dirt" });
            // Complète avec une liste vanilla étendue (évite palette trop courte si blocks.txt minimal)
            DefaultBlockPalette.MergeInto(_paletteAll);
            ApplyPaletteFilter();
            if (PaletteList.Items.Count > 0)
                PaletteList.SelectedIndex = 0;
        }

        private void ApplyPaletteFilter()
        {
            string q = (PaletteSearch?.Text ?? "").Trim().ToLowerInvariant();
            PaletteList.Items.Clear();
            foreach (var id in _paletteAll)
            {
                if (q.Length == 0 || id.ToLowerInvariant().Contains(q))
                    PaletteList.Items.Add(id);
            }
        }

        private void OnPaletteSearch(object sender, TextChangedEventArgs e) => ApplyPaletteFilter();

        private void OnPaletteSelection(object sender, SelectionChangedEventArgs e)
        {
            if (PaletteList.SelectedItem is string s)
            {
                BlockIdBox.Text = s;
                if (StateBox != null)
                    StateBox.Text = BlockStateHelper.RealignStateToBlockId(s, StateBox.Text);
                SetStatus("Bloc : " + s);
            }
        }

        private void OnBlockIdLostFocus(object sender, RoutedEventArgs e)
        {
            if (StateBox == null || BlockIdBox == null) return;
            string id = string.IsNullOrWhiteSpace(BlockIdBox.Text) ? "minecraft:stone" : BlockIdBox.Text.Trim();
            string aligned = BlockStateHelper.RealignStateToBlockId(id, StateBox.Text);
            if (!string.Equals(StateBox.Text.Trim(), aligned, StringComparison.Ordinal))
                StateBox.Text = aligned;
        }

        private void OnToolSelect(object sender, RoutedEventArgs e) => SetTool(EditorTool.Select);
        private void OnToolLine(object sender, RoutedEventArgs e) => SetTool(EditorTool.Line);
        private void OnTool1x1(object sender, RoutedEventArgs e) => SetTool(EditorTool.Block1);
        private void OnTool2x1(object sender, RoutedEventArgs e) => SetTool(EditorTool.Block2x1);
        private void OnTool2x2(object sender, RoutedEventArgs e) => SetTool(EditorTool.Block2x2);
        private void OnToolBrush(object sender, RoutedEventArgs e) => SetTool(EditorTool.Brush3);
        private void OnToolEraser(object sender, RoutedEventArgs e) => SetTool(EditorTool.Eraser);
        private void OnToolFillRect(object sender, RoutedEventArgs e) => SetTool(EditorTool.FillRect);
        private void OnToolWallRect(object sender, RoutedEventArgs e) => SetTool(EditorTool.WallRect);
        private void OnToolFillCircle(object sender, RoutedEventArgs e) => SetTool(EditorTool.FillCircle);
        private void OnToolWallCircle(object sender, RoutedEventArgs e) => SetTool(EditorTool.WallCircle);
        private void OnToolFillEllipse(object sender, RoutedEventArgs e) => SetTool(EditorTool.FillEllipse);
        private void OnToolWallEllipse(object sender, RoutedEventArgs e) => SetTool(EditorTool.WallEllipse);
        private void OnToolZone(object sender, RoutedEventArgs e) => SetTool(EditorTool.ZoneSelect);

        private void SetTool(EditorTool t)
        {
            _tool = t;
            SetStatus("Outil : " + t);
        }

        private void OnNewScene(object sender, RoutedEventArgs e)
        {
            ClearBlocks();
            _history.SeedInitial(_blocks, _currentLayer);
            SetStatus("Nouvelle création");
        }

        private void ClearBlocks()
        {
            for (int i = WorldCanvas.Children.Count - 1; i >= 0; i--)
            {
                var c = WorldCanvas.Children[i];
                if (ReferenceEquals(c, GridLinesCanvas) || ReferenceEquals(c, PreviewHost)) continue;
                WorldCanvas.Children.RemoveAt(i);
            }
            _blocks.Clear();
            _selection.Clear();
            ClearPropertyFields();
        }

        private void ClearPropertyFields()
        {
            XField.Text = "";
            YField.Text = "";
            WField.Text = "";
            HField.Text = "";
        }

        private void OnApplySize(object sender, RoutedEventArgs e)
        {
            if (!TryGetSceneSizeFromTextBoxes(out double w, out double h, out int cw, out int ch, out string? err))
            {
                SetStatus(err ?? "Taille invalide");
                return;
            }
            _sceneWidth = w;
            _sceneHeight = h;
            WorldCanvas.Width = _sceneWidth;
            WorldCanvas.Height = _sceneHeight;
            GridLinesCanvas.Width = _sceneWidth;
            GridLinesCanvas.Height = _sceneHeight;
            if (SceneWidthBox != null) SceneWidthBox.Text = cw.ToString("0", CultureInfo.InvariantCulture);
            if (SceneHeightBox != null) SceneHeightBox.Text = ch.ToString("0", CultureInfo.InvariantCulture);
            SyncPreviewSizes();
            SetStatus("Taille appliquée : " + cw + "×" + ch + " blocs");
            ScheduleGridRedraw();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => ScheduleGridRedraw();

        private void ScheduleGridRedraw()
        {
            if (GridLinesCanvas == null || EditorScroll == null) return;
            _gridDebounce.Stop();
            _gridDebounce.Start();
        }

        private void RedrawViewportGrid()
        {
            if (GridLinesCanvas == null || EditorScroll == null || WorldScale == null) return;
            GridLinesCanvas.Children.Clear();
            double s = Math.Max(MinZoom, WorldScale.ScaleX);
            double vpW = Math.Max(1, EditorScroll.ViewportWidth);
            double vpH = Math.Max(1, EditorScroll.ViewportHeight);
            double visW = vpW / s;
            double visH = vpH / s;
            double minOx = EditorScroll.HorizontalOffset / s;
            double minOy = EditorScroll.VerticalOffset / s;
            double padX = Math.Max(GridSize, visW * 0.1);
            double padY = Math.Max(GridSize, visH * 0.1);
            double startX = Clamp(minOx - padX, 0, _sceneWidth);
            double endX = Clamp(minOx + visW + padX, 0, _sceneWidth);
            double startY = Clamp(minOy - padY, 0, _sceneHeight);
            double endY = Clamp(minOy + visH + padY, 0, _sceneHeight);
            double spanX = endX - startX;
            double spanY = endY - startY;
            const int maxMinor = 520;
            double stepX = spanX / GridSize <= maxMinor ? GridSize : Math.Max(GridSize, Math.Round(spanX / 48.0 / GridSize) * GridSize);
            double stepY = spanY / GridSize <= maxMinor ? GridSize : Math.Max(GridSize, Math.Round(spanY / 48.0 / GridSize) * GridSize);
            const int maxLines = 800;
            if (spanX > 0 && spanX / stepX > maxLines)
            {
                stepX = spanX / (maxLines - 1);
                stepX = Math.Max(GridSize, Math.Round(stepX / GridSize) * GridSize);
            }
            if (spanY > 0 && spanY / stepY > maxLines)
            {
                stepY = spanY / (maxLines - 1);
                stepY = Math.Max(GridSize, Math.Round(stepY / GridSize) * GridSize);
            }
            var major = (Color)ColorConverter.ConvertFromString("#D0D0D0")!;
            var minor = (Color)ColorConverter.ConvertFromString("#B0B0B0")!;
            int xi = 0;
            double x = Math.Floor(startX / stepX) * stepX;
            if (x < startX - 1e-6) x += stepX;
            for (; x <= endX + 1e-6; x += stepX)
            {
                GridLinesCanvas.Children.Add(new Line
                {
                    X1 = x, Y1 = startY, X2 = x, Y2 = endY,
                    Stroke = new SolidColorBrush(xi % 5 == 0 ? major : minor),
                    StrokeThickness = xi % 5 == 0 ? 1.35 : 1,
                    SnapsToDevicePixels = true
                });
                xi++;
            }
            int yi = 0;
            double y = Math.Floor(startY / stepY) * stepY;
            if (y < startY - 1e-6) y += stepY;
            for (; y <= endY + 1e-6; y += stepY)
            {
                GridLinesCanvas.Children.Add(new Line
                {
                    X1 = startX, Y1 = y, X2 = endX, Y2 = y,
                    Stroke = new SolidColorBrush(yi % 5 == 0 ? major : minor),
                    StrokeThickness = yi % 5 == 0 ? 1.35 : 1,
                    SnapsToDevicePixels = true
                });
                yi++;
            }
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        private Point ToWorldPoint(MouseEventArgs e)
        {
            Point p = e.GetPosition(WorldCanvas);
            if (SnapCheck.IsChecked == true)
                p = new Point(GridGeometry.Snap(p.X, GridSize), GridGeometry.Snap(p.Y, GridSize));
            return p;
        }

        private void OnWorldMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            Point p = ToWorldPoint(e);
            // Ne pas capturer la souris pour les outils « clic » : sinon tous les boutons de la fenêtre cessent de répondre.
            if (WorldCanvas.IsMouseCaptured)
                WorldCanvas.ReleaseMouseCapture();

            switch (_tool)
            {
                case EditorTool.Select:
                    HitSelect(p, Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                case EditorTool.Line:
                case EditorTool.FillRect:
                case EditorTool.WallRect:
                case EditorTool.FillCircle:
                case EditorTool.WallCircle:
                case EditorTool.FillEllipse:
                case EditorTool.WallEllipse:
                case EditorTool.ZoneSelect:
                    _dragStart = p;
                    UpdateDragPreview(p, p);
                    WorldCanvas.CaptureMouse();
                    break;
                case EditorTool.Block1:
                    PlaceRectangle(p.X, p.Y, GridSize, GridSize);
                    break;
                case EditorTool.Block2x1:
                    PlaceRectangle(p.X, p.Y, 2 * GridSize, GridSize);
                    break;
                case EditorTool.Block2x2:
                    PlaceRectangle(p.X, p.Y, 2 * GridSize, 2 * GridSize);
                    break;
                case EditorTool.Brush3:
                    PlaceBrush3x3(p);
                    break;
                case EditorTool.Eraser:
                    EraseAt(p);
                    break;
            }
        }

        private void OnWorldMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStart == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            Point p = ToWorldPoint(e);
            UpdateDragPreview(_dragStart.Value, p);
        }

        private void OnWorldLostMouseCapture(object sender, MouseEventArgs e)
        {
            _dragStart = null;
            HideDragPreview();
        }

        private void OnWorldMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            // Ne pas appeler ReleaseMouseCapture() avant d’utiliser _dragStart : sinon LostMouseCapture
            // remet _dragStart à null et aucun outil « glisser » (ligne, zone, formes…) ne s’applique.
            if (_dragStart == null)
            {
                if (WorldCanvas.IsMouseCaptured)
                    WorldCanvas.ReleaseMouseCapture();
                return;
            }

            Point start = _dragStart.Value;
            Point end = ToWorldPoint(e);
            _dragStart = null;
            HideDragPreview();

            switch (_tool)
            {
                case EditorTool.Line:
                    PlaceLine(start, end);
                    break;
                case EditorTool.FillRect:
                    PlaceFilledRectangle(start, end);
                    break;
                case EditorTool.WallRect:
                    PlaceWallRectangle(start, end);
                    break;
                case EditorTool.FillCircle:
                    PlaceFilledCircle(start, end);
                    break;
                case EditorTool.WallCircle:
                    PlaceWallCircle(start, end);
                    break;
                case EditorTool.FillEllipse:
                    PlaceFilledEllipse(start, end);
                    break;
                case EditorTool.WallEllipse:
                    PlaceWallEllipse(start, end);
                    break;
                case EditorTool.ZoneSelect:
                    SelectZone(start, end);
                    break;
            }

            if (WorldCanvas.IsMouseCaptured)
                WorldCanvas.ReleaseMouseCapture();
        }

        private void UpdateDragPreview(Point a, Point b)
        {
            HideDragPreview();
            switch (_tool)
            {
                case EditorTool.Line:
                    DragPreviewLine.X1 = a.X;
                    DragPreviewLine.Y1 = a.Y;
                    DragPreviewLine.X2 = b.X;
                    DragPreviewLine.Y2 = b.Y;
                    DragPreviewLine.Visibility = Visibility.Visible;
                    break;
                case EditorTool.FillRect:
                case EditorTool.WallRect:
                case EditorTool.ZoneSelect:
                {
                    double minX = Math.Min(a.X, b.X);
                    double minY = Math.Min(a.Y, b.Y);
                    double w = Math.Abs(a.X - b.X) + GridSize;
                    double h = Math.Abs(a.Y - b.Y) + GridSize;
                    DragPreviewRect.Width = w;
                    DragPreviewRect.Height = h;
                    Canvas.SetLeft(DragPreviewRect, minX);
                    Canvas.SetTop(DragPreviewRect, minY);
                    DragPreviewRect.Visibility = Visibility.Visible;
                    break;
                }
                case EditorTool.FillCircle:
                case EditorTool.WallCircle:
                {
                    double cx = a.X, cy = a.Y;
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double r = Math.Max(GridSize, Math.Sqrt(dx * dx + dy * dy));
                    DragPreviewEllipse.Width = 2 * r;
                    DragPreviewEllipse.Height = 2 * r;
                    Canvas.SetLeft(DragPreviewEllipse, cx - r);
                    Canvas.SetTop(DragPreviewEllipse, cy - r);
                    DragPreviewEllipse.Visibility = Visibility.Visible;
                    break;
                }
                case EditorTool.FillEllipse:
                case EditorTool.WallEllipse:
                {
                    double minX = Math.Min(a.X, b.X);
                    double minY = Math.Min(a.Y, b.Y);
                    double w = Math.Abs(a.X - b.X) + GridSize;
                    double h = Math.Abs(a.Y - b.Y) + GridSize;
                    DragPreviewEllipse.Width = w;
                    DragPreviewEllipse.Height = h;
                    Canvas.SetLeft(DragPreviewEllipse, minX);
                    Canvas.SetTop(DragPreviewEllipse, minY);
                    DragPreviewEllipse.Visibility = Visibility.Visible;
                    break;
                }
            }
        }

        private void HideDragPreview()
        {
            if (DragPreviewRect != null) DragPreviewRect.Visibility = Visibility.Collapsed;
            if (DragPreviewLine != null) DragPreviewLine.Visibility = Visibility.Collapsed;
            if (DragPreviewEllipse != null) DragPreviewEllipse.Visibility = Visibility.Collapsed;
        }

        private void HitSelect(Point p, bool add)
        {
            var hit = FindTopBlock(p, _currentLayer);
            if (hit == null)
            {
                if (!add) ClearSelectionFull();
                return;
            }
            if (!add)
                ClearSelectionFull();
            _selection.Add(hit);
            RefreshSelectionStroke();
            FillPropertyPanel(hit);
            SetStatus("Sélection : " + _selection.Count);
        }

        private void SelectZone(Point a, Point b)
        {
            double minX = Math.Min(a.X, b.X);
            double maxX = Math.Max(a.X, b.X);
            double minY = Math.Min(a.Y, b.Y);
            double maxY = Math.Max(a.Y, b.Y);
            ClearSelectionFull();
            foreach (var blk in _blocks)
            {
                if (blk.Layer != _currentLayer) continue;
                if (blk.X >= minX && blk.Y >= minY && blk.X + blk.Width <= maxX + GridSize && blk.Y + blk.Height <= maxY + GridSize)
                    _selection.Add(blk);
            }
            RefreshSelectionStroke();
            SetStatus("Zone : " + _selection.Count);
        }

        /// <summary>Retire uniquement les contours de sélection (ne vide pas <see cref="_selection"/>).</summary>
        private void StripSelectionStrokes()
        {
            foreach (var r in EnumerateBlockRects())
            {
                if (r.Tag is PlacedBlock b && _selection.Contains(b))
                {
                    r.Stroke = Brushes.Transparent;
                    r.StrokeThickness = 0;
                }
            }
        }

        private void ClearSelectionFull()
        {
            StripSelectionStrokes();
            _selection.Clear();
        }

        private void RefreshSelectionStroke()
        {
            foreach (var r in EnumerateBlockRects())
            {
                if (r.Tag is PlacedBlock b)
                {
                    if (_selection.Contains(b))
                    {
                        r.Stroke = _selection.Count > 1 ? Brushes.Gold : Brushes.White;
                        r.StrokeThickness = _selection.Count > 1 ? 2.5 : 2;
                    }
                    else
                    {
                        r.Stroke = Brushes.Transparent;
                        r.StrokeThickness = 0;
                    }
                }
            }
        }

        private IEnumerable<Rectangle> EnumerateBlockRects()
        {
            foreach (var c in WorldCanvas.Children)
            {
                if (c is Rectangle rx && !ReferenceEquals(rx, DragPreviewRect))
                    yield return rx;
            }
        }

        /// <summary>Comme <c>HelloController.refreshLayerHintMarkers</c> (JavaFX) : miroir des blocs du calque <b>n-1</b> sur le calque <b>n</b>, 1 case = 1 image, seulement là où c’est <b>vide</b> sur n.</summary>
        private static string? FindLayerHintImageFilePath()
        {
            string dir = AppContext.BaseDirectory;
            foreach (string name in new[] { "layer_block.png", "layer_block.jpg", "layer_block.jpeg", "layer_hint.png", "stack_marker.png", "stack_marker.jpg", "stack_marker.jpeg", "stack_marker.bmp", "stack_marker", "layer_block" })
            {
                string p = IoPath.Combine(dir, name);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private ImageSource GetLayerHintImageSource()
        {
            if (_layerHintImageSource != null) return _layerHintImageSource;
            try
            {
                string? path = FindLayerHintImageFilePath();
                if (path != null)
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(path, UriKind.Absolute);
                    bi.EndInit();
                    if (bi.CanFreeze) bi.Freeze();
                    _layerHintImageSource = bi;
                    return _layerHintImageSource;
                }
            }
            catch { /* fallback */ }

            // Ressource intégrée fournie avec l'app.
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri("pack://application:,,,/Assets/layer_block.png", UriKind.Absolute);
                bi.EndInit();
                if (bi.CanFreeze) bi.Freeze();
                _layerHintImageSource = bi;
                return _layerHintImageSource;
            }
            catch { /* fallback généré */ }
            // Carré blanc bordure gris si aucun layer_block / layer_hint / stack_marker près de l’exe
            const int n = 32;
            var wb = new WriteableBitmap(n, n, 96, 96, PixelFormats.Pbgra32, null);
            int stride = 4 * n;
            var px = new byte[stride * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int j = y * stride + x * 4;
                bool edge = x < 1 || y < 1 || x > n - 2 || y > n - 2;
                byte g = edge ? (byte)120 : (byte)255;
                px[j] = g;
                px[j + 1] = g;
                px[j + 2] = g;
                px[j + 3] = 255;
            }
            wb.WritePixels(new Int32Rect(0, 0, n, n), px, stride, 0);
            if (wb.CanFreeze) wb.Freeze();
            _layerHintImageSource = wb;
            return _layerHintImageSource;
        }

        private void RemoveAllLayerHintImages()
        {
            if (WorldCanvas == null) return;
            for (int i = WorldCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (WorldCanvas.Children[i] is System.Windows.Controls.Image img
                    && ReferenceEquals(img.Tag, LayerHintViewTag))
                    WorldCanvas.Children.RemoveAt(i);
            }
        }

        private void RefreshLayerHintMarkers()
        {
            if (WorldCanvas == null) return;
            RemoveAllLayerHintImages();
            if (PreviewHost == null) return;
            // Java : if (currentLayer <= 0) return; — rien en calque 0
            if (_currentLayer <= 0) return;

            int z = WorldCanvas.Children.IndexOf(PreviewHost);
            if (z < 0) return;

            ImageSource src = GetLayerHintImageSource();
            int below = _currentLayer - 1;

            foreach (var b in _blocks)
            {
                if (b.Layer != below) continue;
                int startX = (int)(b.X / GridSize);
                int startY = (int)(b.Y / GridSize);
                int w = Math.Max(1, (int)(b.Width / GridSize));
                int h = Math.Max(1, (int)(b.Height / GridSize));
                for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                {
                    double cellX = (startX + dx) * GridSize;
                    double cellY = (startY + dy) * GridSize;
                    if (!CellFree(cellX, cellY, _currentLayer)) continue;

                    var img = new System.Windows.Controls.Image
                    {
                        Source = src,
                        Width = GridSize,
                        Height = GridSize,
                        IsHitTestVisible = false,
                        Opacity = 0.65,
                        Stretch = Stretch.Fill,
                        Tag = LayerHintViewTag,
                        SnapsToDevicePixels = true
                    };
                    Canvas.SetLeft(img, cellX);
                    Canvas.SetTop(img, cellY);
                    // Comme toBack() en Java : sous PreviewHost et les Rectangle des blocs (insertion avant l’hôte d’aperçu)
                    WorldCanvas.Children.Insert(z, img);
                    z++;
                }
            }
        }

        private PlacedBlock? FindTopBlock(Point p, int layer)
        {
            for (int i = _blocks.Count - 1; i >= 0; i--)
            {
                var b = _blocks[i];
                if (b.Layer != layer) continue;
                if (p.X >= b.X && p.X < b.X + b.Width && p.Y >= b.Y && p.Y < b.Y + b.Height)
                    return b;
            }
            return null;
        }

        private bool CellFree(double x, double y, int layer)
        {
            return !_blocks.Any(b => b.Layer == layer && x >= b.X && x < b.X + b.Width && y >= b.Y && y < b.Y + b.Height);
        }

        private void PlaceLine(Point a, Point b)
        {
            var fill = TryParseColor(ColorHexBox.Text.Trim());
            string id = string.IsNullOrWhiteSpace(BlockIdBox.Text) ? "minecraft:stone" : BlockIdBox.Text.Trim();
            string st = BlockStateHelper.RealignStateToBlockId(id, StateBox.Text);
            bool changed = false;
            foreach (var pt in GridGeometry.LineCells(a, b, GridSize))
            {
                if (!CellFree(pt.X, pt.Y, _currentLayer)) continue;
                var m = new PlacedBlock { X = pt.X, Y = pt.Y, Width = GridSize, Height = GridSize, Layer = _currentLayer, BlockId = id, BlockState = st, Fill = fill };
                _blocks.Add(m);
                InsertRectVisual(m);
                changed = true;
            }
            if (changed) AfterMutation("ligne");
        }

        private void PlaceFilledRectangle(Point a, Point b)
        {
            double minX = Math.Min(a.X, b.X);
            double maxX = Math.Max(a.X, b.X);
            double minY = Math.Min(a.Y, b.Y);
            double maxY = Math.Max(a.Y, b.Y);
            bool changed = false;
            for (double x = minX; x <= maxX; x += GridSize)
            for (double y = minY; y <= maxY; y += GridSize)
            {
                if (!CellFree(x, y, _currentLayer)) continue;
                AddCell(x, y, ref changed);
            }
            if (changed) AfterMutation("rectangle plein");
        }

        private void PlaceWallRectangle(Point a, Point b)
        {
            double minX = Math.Min(a.X, b.X);
            double maxX = Math.Max(a.X, b.X);
            double minY = Math.Min(a.Y, b.Y);
            double maxY = Math.Max(a.Y, b.Y);
            bool changed = false;
            for (double x = minX; x <= maxX; x += GridSize)
            for (double y = minY; y <= maxY; y += GridSize)
            {
                bool border = x <= minX + 1e-6 || x >= maxX - 1e-6 || y <= minY + 1e-6 || y >= maxY - 1e-6;
                if (!border || !CellFree(x, y, _currentLayer)) continue;
                AddCell(x, y, ref changed);
            }
            if (changed) AfterMutation("rectangle mur");
        }

        private void PlaceFilledCircle(Point start, Point end)
        {
            double cx = start.X, cy = start.Y;
            double dx = end.X - start.X, dy = end.Y - start.Y;
            double radius = Math.Max(GridSize, Math.Sqrt(dx * dx + dy * dy));
            bool changed = false;
            for (double x = cx - radius; x <= cx + radius; x += GridSize)
            for (double y = cy - radius; y <= cy + radius; y += GridSize)
            {
                double px = x + GridSize / 2, py = y + GridSize / 2;
                if (Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) > radius) continue;
                double gx = GridGeometry.Snap(x, GridSize), gy = GridGeometry.Snap(y, GridSize);
                if (!CellFree(gx, gy, _currentLayer)) continue;
                AddCell(gx, gy, ref changed);
            }
            if (changed) AfterMutation("cercle plein");
        }

        private void PlaceWallCircle(Point start, Point end)
        {
            double cx = start.X, cy = start.Y;
            double dx = end.X - start.X, dy = end.Y - start.Y;
            double radius = Math.Max(GridSize, Math.Sqrt(dx * dx + dy * dy));
            double half = GridSize * 0.5;
            bool changed = false;
            for (double x = cx - radius - GridSize; x <= cx + radius + GridSize; x += GridSize)
            for (double y = cy - radius - GridSize; y <= cy + radius + GridSize; y += GridSize)
            {
                double px = x + GridSize / 2, py = y + GridSize / 2;
                double dist = Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                if (Math.Abs(dist - radius) > half) continue;
                double gx = GridGeometry.Snap(x, GridSize), gy = GridGeometry.Snap(y, GridSize);
                if (!CellFree(gx, gy, _currentLayer)) continue;
                AddCell(gx, gy, ref changed);
            }
            if (changed) AfterMutation("cercle mur");
        }

        private void PlaceFilledEllipse(Point start, Point end)
        {
            double minX = Math.Min(start.X, end.X), maxX = Math.Max(start.X, end.X);
            double minY = Math.Min(start.Y, end.Y), maxY = Math.Max(start.Y, end.Y);
            double centerX = (minX + maxX) / 2, centerY = (minY + maxY) / 2;
            double rx = Math.Max(GridSize / 2, (maxX - minX) / 2);
            double ry = Math.Max(GridSize / 2, (maxY - minY) / 2);
            bool changed = false;
            for (double x = minX; x <= maxX; x += GridSize)
            for (double y = minY; y <= maxY; y += GridSize)
            {
                double px = x + GridSize / 2, py = y + GridSize / 2;
                double nx = (px - centerX) / rx, ny = (py - centerY) / ry;
                if (nx * nx + ny * ny > 1) continue;
                double gx = GridGeometry.Snap(x, GridSize), gy = GridGeometry.Snap(y, GridSize);
                if (!CellFree(gx, gy, _currentLayer)) continue;
                AddCell(gx, gy, ref changed);
            }
            if (changed) AfterMutation("ellipse pleine");
        }

        private void PlaceWallEllipse(Point start, Point end)
        {
            double minX = Math.Min(start.X, end.X), maxX = Math.Max(start.X, end.X);
            double minY = Math.Min(start.Y, end.Y), maxY = Math.Max(start.Y, end.Y);
            double centerX = (minX + maxX) / 2, centerY = (minY + maxY) / 2;
            double rx = Math.Max(GridSize / 2, (maxX - minX) / 2);
            double ry = Math.Max(GridSize / 2, (maxY - minY) / 2);
            bool changed = false;
            for (double x = minX - GridSize; x <= maxX + GridSize; x += GridSize)
            for (double y = minY - GridSize; y <= maxY + GridSize; y += GridSize)
            {
                double px = x + GridSize / 2, py = y + GridSize / 2;
                double nx = (px - centerX) / rx, ny = (py - centerY) / ry;
                double d = nx * nx + ny * ny;
                if (Math.Abs(d - 1.0) > 0.28) continue;
                double gx = GridGeometry.Snap(x, GridSize), gy = GridGeometry.Snap(y, GridSize);
                if (!CellFree(gx, gy, _currentLayer)) continue;
                AddCell(gx, gy, ref changed);
            }
            if (changed) AfterMutation("ellipse mur");
        }

        private void AddCell(double x, double y, ref bool changed)
        {
            var fill = TryParseColor(ColorHexBox.Text.Trim());
            string id = string.IsNullOrWhiteSpace(BlockIdBox.Text) ? "minecraft:stone" : BlockIdBox.Text.Trim();
            string st = BlockStateHelper.RealignStateToBlockId(id, StateBox.Text);
            var m = new PlacedBlock { X = x, Y = y, Width = GridSize, Height = GridSize, Layer = _currentLayer, BlockId = id, BlockState = st, Fill = fill };
            _blocks.Add(m);
            InsertRectVisual(m);
            changed = true;
        }

        private void PlaceRectangle(double x, double y, double w, double h)
        {
            if (!CellFree(x, y, _currentLayer)) return;
            bool ok = true;
            for (double xx = x; xx < x + w && ok; xx += GridSize)
            for (double yy = y; yy < y + h && ok; yy += GridSize)
            {
                if (!CellFree(xx, yy, _currentLayer)) ok = false;
            }
            if (!ok) return;
            var fill = TryParseColor(ColorHexBox.Text.Trim());
            string bid = string.IsNullOrWhiteSpace(BlockIdBox.Text) ? "minecraft:stone" : BlockIdBox.Text.Trim();
            string bst = BlockStateHelper.RealignStateToBlockId(bid, StateBox.Text);
            var m = new PlacedBlock
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                Layer = _currentLayer,
                BlockId = bid,
                BlockState = bst,
                Fill = fill
            };
            _blocks.Add(m);
            InsertRectVisual(m);
            AfterMutation("bloc");
        }

        private void PlaceBrush3x3(Point center)
        {
            double sx = center.X - GridSize;
            double sy = center.Y - GridSize;
            bool changed = false;
            for (int gx = 0; gx < 3; gx++)
            for (int gy = 0; gy < 3; gy++)
            {
                double x = sx + gx * GridSize, y = sy + gy * GridSize;
                if (!CellFree(x, y, _currentLayer)) continue;
                AddCell(x, y, ref changed);
            }
            if (changed) AfterMutation("pinceau 3×3");
        }

        private void EraseAt(Point p)
        {
            var hit = FindTopBlock(p, _currentLayer);
            if (hit == null) return;
            RemoveBlock(hit);
            AfterMutation("gomme");
        }

        private void RemoveBlock(PlacedBlock b)
        {
            _blocks.Remove(b);
            _selection.Remove(b);
            foreach (var r in EnumerateBlockRects().ToList())
            {
                if (ReferenceEquals(r.Tag, b))
                {
                    WorldCanvas.Children.Remove(r);
                    break;
                }
            }
        }

        private void InsertRectVisual(PlacedBlock b)
        {
            var r = CreateRectVisual(b);
            int idx = PreviewInsertIndex;
            if (idx < 0) WorldCanvas.Children.Add(r);
            else WorldCanvas.Children.Insert(idx, r);
        }

        private Rectangle CreateRectVisual(PlacedBlock b)
        {
            var r = new Rectangle
            {
                Width = b.Width,
                Height = b.Height,
                Fill = new SolidColorBrush(b.Fill),
                Stroke = Brushes.Transparent,
                StrokeThickness = 0,
                Tag = b,
                IsHitTestVisible = true,
                Visibility = b.Layer == _currentLayer ? Visibility.Visible : Visibility.Collapsed
            };
            Canvas.SetLeft(r, b.X);
            Canvas.SetTop(r, b.Y);
            return r;
        }

        /// <summary>Affiche uniquement les blocs du calque actif (comme refreshLayerVisibility côté Java).</summary>
        private void RefreshLayerVisibility()
        {
            foreach (var r in EnumerateBlockRects())
            {
                if (r.Tag is PlacedBlock b)
                    r.Visibility = b.Layer == _currentLayer ? Visibility.Visible : Visibility.Collapsed;
            }
            ScheduleGridRedraw();
            RefreshLayerHintMarkers();
            RefreshDirectionOverlays();
        }

        private void AfterMutation(string msg)
        {
            _history.PushAfterChange(_blocks, _currentLayer);
            SetStatus(msg);
            ScheduleGridRedraw();
            RefreshLayerHintMarkers();
            RefreshDirectionOverlays();
        }

        private void OnUndo(object sender, RoutedEventArgs e)
        {
            int layer = _currentLayer;
            if (!_history.TryUndo(_blocks, ref layer)) { SetStatus("rien à annuler"); return; }
            _currentLayer = layer;
            RebuildAllVisuals();
            UpdateLayerLabel();
            SetStatus("undo");
        }

        private void OnRedo(object sender, RoutedEventArgs e)
        {
            int layer = _currentLayer;
            if (!_history.TryRedo(_blocks, ref layer)) { SetStatus("rien à refaire"); return; }
            _currentLayer = layer;
            RebuildAllVisuals();
            UpdateLayerLabel();
            SetStatus("redo");
        }

        private void RebuildAllVisuals()
        {
            RemoveAllLayerHintImages();
            RemoveAllDirectionOverlays();
            foreach (var r in EnumerateBlockRects().ToList())
                WorldCanvas.Children.Remove(r);
            foreach (var b in _blocks)
                InsertRectVisual(b);
            RefreshSelectionStroke();
            RefreshLayerHintMarkers();
            RefreshDirectionOverlays();
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            _clipboard.Clear();
            foreach (var b in _selection)
                _clipboard.Add(b.Clone());
            SetStatus("copié : " + _clipboard.Count);
        }

        private void OnPaste(object sender, RoutedEventArgs e)
        {
            if (_clipboard.Count == 0) { SetStatus("presse-papiers vide"); return; }
            ClearSelectionFull();
            foreach (var c in _clipboard)
            {
                var p = c.Clone();
                p.BlockState = BlockStateHelper.RealignStateToBlockId(p.BlockId, p.BlockState);
                p.X = GridGeometry.Snap(p.X + GridSize, GridSize);
                p.Y = GridGeometry.Snap(p.Y + GridSize, GridSize);
                p.Layer = _currentLayer;
                if (p.X + p.Width > _sceneWidth || p.Y + p.Height > _sceneHeight) continue;
                _blocks.Add(p);
                InsertRectVisual(p);
                _selection.Add(p);
            }
            AfterMutation("collé");
            RefreshSelectionStroke();
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (_selection.Count == 0) { SetStatus("rien à supprimer"); return; }
            foreach (var b in _selection.ToList())
                RemoveBlock(b);
            _selection.Clear();
            AfterMutation("supprimé");
        }

        private void OnApplyProps(object sender, RoutedEventArgs e)
        {
            if (_selection.Count != 1) { SetStatus("sélectionnez un seul bloc"); return; }
            var b = _selection.First();
            if (double.TryParse(XField.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double nx))
                b.X = GridGeometry.Snap(nx, GridSize);
            if (double.TryParse(YField.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double ny))
                b.Y = GridGeometry.Snap(ny, GridSize);
            if (double.TryParse(WField.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double nw))
                b.Width = Math.Max(GridSize, GridGeometry.Snap(nw, GridSize));
            if (double.TryParse(HField.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double nh))
                b.Height = Math.Max(GridSize, GridGeometry.Snap(nh, GridSize));
            b.BlockId = string.IsNullOrWhiteSpace(BlockIdBox.Text) ? "minecraft:stone" : BlockIdBox.Text.Trim();
            b.BlockState = BlockStateHelper.RealignStateToBlockId(b.BlockId, StateBox.Text);
            StateBox.Text = b.BlockState;
            b.Fill = TryParseColor(ColorHexBox.Text.Trim());
            RebuildAllVisuals();
            RefreshSelectionStroke();
            AfterMutation("propriétés");
        }

        private void FillPropertyPanel(PlacedBlock b)
        {
            XField.Text = b.X.ToString("0", CultureInfo.InvariantCulture);
            YField.Text = b.Y.ToString("0", CultureInfo.InvariantCulture);
            WField.Text = b.Width.ToString("0", CultureInfo.InvariantCulture);
            HField.Text = b.Height.ToString("0", CultureInfo.InvariantCulture);
            BlockIdBox.Text = b.BlockId;
            StateBox.Text = BlockStateHelper.RealignStateToBlockId(b.BlockId, b.BlockState);
            _suppressColorEvent = true;
            try
            {
                ColorHexBox.Text = "#" + b.Fill.R.ToString("X2") + b.Fill.G.ToString("X2") + b.Fill.B.ToString("X2");
                string hx = ColorHexBox.Text;
                if (_colorPresets != null)
                {
                    int found = -1;
                    for (int i = 0; i < _colorPresets.Length; i++)
                    {
                        if (string.Equals(_colorPresets[i].hex, hx, StringComparison.OrdinalIgnoreCase)) { found = i; break; }
                    }
                    if (ColorPresetCombo != null) ColorPresetCombo.SelectedIndex = found;
                }
            }
            finally { _suppressColorEvent = false; }
        }

        private void OnRotate90(object sender, RoutedEventArgs e) => ApplyRotationToSelection(true);

        private void OnRotateMinus90(object sender, RoutedEventArgs e) => ApplyRotationToSelection(false);

        private void OnAxisX(object sender, RoutedEventArgs e) => ApplyAxisToSelection('x');
        private void OnAxisY(object sender, RoutedEventArgs e) => ApplyAxisToSelection('y');
        private void OnAxisZ(object sender, RoutedEventArgs e) => ApplyAxisToSelection('z');

        private void ApplyRotationToSelection(bool positive)
        {
            if (_selection.Count == 0) { SetStatus("Sélection vide (rotation)"); return; }
            foreach (var b in _selection)
                b.BlockState = BlockStateHelper.RotateY90(b.BlockId, b.BlockState, positive);
            if (_selection.Count == 1) StateBox.Text = _selection.First().BlockState;
            AfterMutation(positive ? "Rotation +90° (facing)" : "Rotation −90° (facing)");
        }

        private void ApplyAxisToSelection(char ax)
        {
            if (_selection.Count == 0) { SetStatus("Sélection vide (axe)"); return; }
            int n = 0;
            foreach (var b in _selection)
            {
                if (!BlockStateHelper.SupportsAxis(b.BlockId)) continue;
                b.BlockState = BlockStateHelper.SetAxisXyz(b.BlockId, b.BlockState, ax);
                n++;
            }
            if (n == 0)
            {
                SetStatus("Axe : ce bloc ne gère pas les axes (bûches, escaliers, bambou, piliers… — pas la pierre seule).");
                return;
            }
            if (_selection.Count == 1) StateBox.Text = _selection.First().BlockState;
            AfterMutation("Axe " + char.ToUpperInvariant(ax) + " — " + n + " bloc(s)");
        }

        private void RemoveAllDirectionOverlays()
        {
            if (WorldCanvas == null) return;
            for (int i = WorldCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (WorldCanvas.Children[i] is TextBlock tb && ReferenceEquals(tb.Tag, DirectionOverlayTag))
                    WorldCanvas.Children.RemoveAt(i);
            }
        }

        private void RefreshDirectionOverlays()
        {
            if (WorldCanvas == null) return;
            RemoveAllDirectionOverlays();
            foreach (var b in _blocks)
            {
                if (b.Layer != _currentLayer) continue;
                string g = BlockStateHelper.GetDirectionGlyph(b.BlockId, b.BlockState);
                if (g.Length == 0) continue;
                double luma = (0.299 * b.Fill.R + 0.587 * b.Fill.G + 0.114 * b.Fill.B) / 255.0;
                var fore = luma > 0.55 ? Brushes.Black : Brushes.White;
                double fs = Math.Max(10, Math.Min(24, Math.Min(b.Width, b.Height) * 0.4));
                var tb = new TextBlock
                {
                    Text = g,
                    Foreground = fore,
                    FontSize = fs,
                    FontWeight = FontWeights.SemiBold,
                    Tag = DirectionOverlayTag,
                    IsHitTestVisible = false
                };
                tb.Measure(new Size(b.Width, b.Height));
                Canvas.SetLeft(tb, b.X + (b.Width - tb.DesiredSize.Width) * 0.5);
                Canvas.SetTop(tb, b.Y + (b.Height - tb.DesiredSize.Height) * 0.5);
                WorldCanvas.Children.Add(tb);
            }
        }

        private static Color TryParseColor(string hex)
        {
            try
            {
                if (!hex.StartsWith("#", StringComparison.Ordinal)) hex = "#" + hex;
                return (Color)ColorConverter.ConvertFromString(hex)!;
            }
            catch { return Color.FromRgb(0xBF, 0xBF, 0xBF); }
        }

        private void OnLayerUp(object sender, RoutedEventArgs e)
        {
            _currentLayer = Math.Min(255, _currentLayer + 1);
            ClearSelectionFull();
            UpdateLayerLabel();
            RefreshLayerVisibility();
            SetStatus("Calque " + _currentLayer);
        }

        private void OnLayerDown(object sender, RoutedEventArgs e)
        {
            _currentLayer = Math.Max(0, _currentLayer - 1);
            ClearSelectionFull();
            UpdateLayerLabel();
            RefreshLayerVisibility();
            SetStatus("Calque " + _currentLayer);
        }

        private void UpdateLayerLabel()
        {
            string s = _currentLayer.ToString(CultureInfo.InvariantCulture);
            LayerText.Text = s;
            LayerSummaryText.Text = string.Format(CultureInfo.InvariantCulture, Localization.T("Layer_SummaryFmt"), s);
        }

        private void OnZoomSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressZoom) return;
            // Pendant InitializeComponent(), le Slider peut lever ValueChanged avant que WorldScale existe.
            if (WorldScale == null) return;
            double z = Clamp(e.NewValue, MinZoom, MaxZoom);
            WorldScale.ScaleX = z;
            WorldScale.ScaleY = z;
            UpdateZoomLabel();
            ScheduleGridRedraw();
        }

        private void OnZoomIn(object sender, RoutedEventArgs e) => BumpZoom(ZoomStep);
        private void OnZoomOut(object sender, RoutedEventArgs e) => BumpZoom(1 / ZoomStep);

        private void BumpZoom(double factor)
        {
            if (WorldScale == null || ZoomSlider == null) return;
            double z = Clamp(WorldScale.ScaleX * factor, MinZoom, MaxZoom);
            _suppressZoom = true;
            try { ZoomSlider.Value = z; }
            finally { _suppressZoom = false; }
            WorldScale.ScaleX = z;
            WorldScale.ScaleY = z;
            UpdateZoomLabel();
            ScheduleGridRedraw();
        }

        private void OnEditorScrollWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;
            BumpZoom(e.Delta > 0 ? ZoomStep : 1 / ZoomStep);
        }

        private void UpdateZoomLabel()
        {
            if (ZoomLabel == null || WorldScale == null) return;
            ZoomLabel.Text = string.Format(CultureInfo.InvariantCulture, Localization.T("Zoom_LabelFmt"), (int)Math.Round(WorldScale.ScaleX * 100));
        }

        private void SetStatus(string s) => StatusText.Text = Localization.T("Status_Prefix") + s;

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && WorldCanvas != null && WorldCanvas.IsMouseCaptured)
            {
                WorldCanvas.ReleaseMouseCapture();
                _dragStart = null;
                HideDragPreview();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { OnUndo(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { OnRedo(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C) { OnCopy(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V) { OnPaste(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (e.Key == Key.Delete) { OnDelete(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (_selection.Count > 0 && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down))
            {
                double dx = e.Key == Key.Left ? -GridSize : e.Key == Key.Right ? GridSize : 0;
                double dy = e.Key == Key.Up ? -GridSize : e.Key == Key.Down ? GridSize : 0;
                foreach (var b in _selection.ToList())
                {
                    b.X = Clamp(GridGeometry.Snap(b.X + dx, GridSize), 0, _sceneWidth - b.Width);
                    b.Y = Clamp(GridGeometry.Snap(b.Y + dy, GridSize), 0, _sceneHeight - b.Height);
                }
                RebuildAllVisuals();
                RefreshSelectionStroke();
                AfterMutation("déplacement");
                e.Handled = true;
            }
        }

        private void OnSaveLegacy(object sender, RoutedEventArgs e) => SaveJson(false, false);
        private void OnSaveCompact(object sender, RoutedEventArgs e) => SaveJson(true, false);
        private void OnSaveWrapper(object sender, RoutedEventArgs e) => SaveJson(true, true);

        private void SaveJson(bool compact, bool wrapper)
        {
            string name = SanitizeName(SceneNameBox.Text);
            var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = wrapper ? name + "_saveddata.json" : compact ? name + "_compact.json" : name + ".json" };
            if (dlg.ShowDialog() != true) return;
            string path = dlg.FileName;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) path += ".json";
            string json = wrapper ? StructureJson.BuildWrapper(name, _sceneWidth, _sceneHeight, _blocks)
                : compact ? StructureJson.BuildCompact(name, _sceneWidth, _sceneHeight, _blocks)
                : StructureJson.BuildLegacy(name, _sceneWidth, _sceneHeight, _blocks);
            try
            {
                StructureJson.WriteFileAtomic(path, json);
                SetStatus("sauvegardé : " + IoPath.GetFileName(path));
            }
            catch (Exception ex) { SetStatus("erreur : " + ex.Message); }
        }

        private static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "structure";
            var t = raw.Trim();
            foreach (var c in IoPath.GetInvalidFileNameChars())
                t = t.Replace(c.ToString(), "", StringComparison.Ordinal);
            return t.Length == 0 ? "structure" : t;
        }

        private void OnLoadLegacy(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON|*.json|All|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                string json = File.ReadAllText(dlg.FileName);
                var list = StructureJson.TryLoadLegacy(json, out string? nm, out double sw, out double sh);
                SceneNameBox.Text = nm ?? "structure";
                int scw = Math.Max(1, (int)Math.Round(sw / GridSize));
                int sch = Math.Max(1, (int)Math.Round(sh / GridSize));
                SceneWidthBox.Text = scw.ToString("0", CultureInfo.InvariantCulture);
                SceneHeightBox.Text = sch.ToString("0", CultureInfo.InvariantCulture);
                _sceneWidth = sw;
                _sceneHeight = sh;
                WorldCanvas.Width = _sceneWidth;
                WorldCanvas.Height = _sceneHeight;
                GridLinesCanvas.Width = _sceneWidth;
                GridLinesCanvas.Height = _sceneHeight;
                SyncPreviewSizes();
                ClearBlocks();
                foreach (var b in list)
                {
                    _blocks.Add(b);
                    InsertRectVisual(b);
                }
                _history.SeedInitial(_blocks, _currentLayer);
                SetStatus("chargé : " + list.Count);
                ScheduleGridRedraw();
                RefreshLayerHintMarkers();
                RefreshDirectionOverlays();
            }
            catch (Exception ex) { SetStatus("erreur : " + ex.Message); }
        }
    }
}
