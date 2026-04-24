using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfGroupBox = System.Windows.Controls.GroupBox;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace MagicalChalkStudio
{
    public static class Localization
    {
        public const string DefaultLanguage = "fr";

        public static string CurrentLanguage { get; private set; } = DefaultLanguage;

        public static event Action? LanguageChanged;

        private static readonly Dictionary<string, Dictionary<string, string>> Dictionaries = new(StringComparer.OrdinalIgnoreCase)
        {
            ["fr"] = new(StringComparer.Ordinal)
            {
                ["LangList"] = "Langue",
                ["Lbl_Language"] = "Langue",
                ["Status_Prefix"] = "Statut : ",
                ["Msg_Ready"] = "prêt",
                ["WindowTitle"] = "Magical Chalk Studio",
                ["Layer_SummaryFmt"] = "Calque actif : {0} / 255 — seul ce calque est visible sur la grille.",
                ["Zoom_LabelFmt"] = "Zoom : {0}%",
                ["Lbl_SizeBlocks__tip"] = "Nombre de cases de la grille (1 bloc = 50 unités internes), comme sizeX / sizeZ dans le JSON.",
                ["Tip_SceneW"] = "Largeur de la scène en blocs (cases)",
                ["Tip_SceneH"] = "Hauteur de la scène en blocs (cases)",
                ["Tip_Hex"] = "Raccourci pour export ; la liste est plus simple",
                ["Tip_ColorPick"] = "Palette système (nuancier)",
                ["Tip_LayerUpSide"] = "Augmenter le calque",
                ["Tip_LayerDownSide"] = "Diminuer le calque",
                ["Layer_PlusSide"] = "Calque +",
                ["Layer_MinusSide"] = "Calque −",
                ["Layer_PlusSide__tip"] = "Augmenter le calque",
                ["Layer_MinusSide__tip"] = "Diminuer le calque",
                ["Tool_Select"] = "Sélection",
                ["Tool_Line"] = "Ligne",
                ["Tool_1x1"] = "1×1",
                ["Tool_2x1"] = "2×1",
                ["Tool_2x2"] = "2×2",
                ["Tool_Brush"] = "Pinceau 3×3",
                ["Tool_Eraser"] = "Gomme",
                ["Tool_FillRect"] = "Fill Rect",
                ["Tool_WallRect"] = "Wall Rect",
                ["Tool_FillCircle"] = "Fill Cercle",
                ["Tool_WallCircle"] = "Wall Cercle",
                ["Tool_FillEllipse"] = "Fill Ellipse",
                ["Tool_WallEllipse"] = "Wall Ellipse",
                ["Tool_Zone"] = "Zone",
                ["Edit_Undo"] = "Annuler",
                ["Edit_Redo"] = "Refaire",
                ["Edit_Copy"] = "Copier",
                ["Edit_Paste"] = "Coller",
                ["Edit_Apply"] = "Appliquer",
                ["Edit_Delete"] = "Supprimer",
                ["Edit_NewScene"] = "Nouvelle création",
                ["Rot_Plus"] = "Rotation +",
                ["Rot_Minus"] = "Rotation −",
                ["Axis_X"] = "Axe X",
                ["Axis_Y"] = "Axe Y",
                ["Axis_Z"] = "Axe Z",
                ["Lbl_Scene"] = "Scène",
                ["Lbl_SizeBlocks"] = "Taille (blocs) L×H",
                ["Btn_ApplySize"] = "Appliquer taille",
                ["Lbl_Block"] = "Block",
                ["Lbl_State"] = "State",
                ["Lbl_Color"] = "Couleur",
                ["Lbl_Hex"] = "hex",
                ["Snap_Grid"] = "Snap grille",
                ["File_SaveLegacy"] = "Save Legacy",
                ["File_SaveCompact"] = "Save Compact",
                ["File_SaveWrapper"] = "Save Wrapper",
                ["File_LoadLegacy"] = "Load Legacy",
                ["Lbl_Layer"] = "Calque :",
                ["Layer_Plus"] = "Calque +",
                ["Layer_Plus__tip"] = "Calque suivant (0–255)",
                ["Layer_Minus"] = "Calque −",
                ["Layer_Minus__tip"] = "Calque précédent",
                ["Zoom_Out"] = "Zoom −",
                ["Zoom_Out__tip"] = "Zoom arrière",
                ["Zoom_In"] = "Zoom +",
                ["Zoom_In__tip"] = "Zoom avant",
                ["Grp_Layers"] = "Calques",
                ["Layer_Shortcuts"] = "Raccourcis : Ctrl+Z / Ctrl+Y, Ctrl+C / Ctrl+V, flèches (sélection), Suppr.",
                ["Layer_HintHelp"] = "Layer hint (comme Java) : dès le calque 1, on voit l’image sur les cases vides du calque actif qui correspondent à un bloc du calque d’en dessous (filet d’alignement). Fichier optionnel : layer_block.png, layer_hint.png ou stack_marker.png à côté de l’exe. Calque 0 : pas d’indicateur.",
                ["Grp_Palette"] = "Palette blocs",
            },
            ["en"] = new(StringComparer.Ordinal)
            {
                ["LangList"] = "Language",
                ["Lbl_Language"] = "Language",
                ["Status_Prefix"] = "Status: ",
                ["Msg_Ready"] = "ready",
                ["WindowTitle"] = "Magical Chalk Studio",
                ["Layer_SummaryFmt"] = "Active layer: {0} / 255 — only this layer is visible on the grid.",
                ["Zoom_LabelFmt"] = "Zoom: {0}%",
                ["Lbl_SizeBlocks__tip"] = "Scene size in grid cells (1 block = 50 internal units), like sizeX / sizeZ in JSON.",
                ["Tip_SceneW"] = "Scene width in blocks (cells)",
                ["Tip_SceneH"] = "Scene height in blocks (cells)",
                ["Tip_Hex"] = "Hex shortcut for export; presets are easier.",
                ["Tip_ColorPick"] = "System color picker",
                ["Tip_LayerUpSide"] = "Increase layer",
                ["Tip_LayerDownSide"] = "Decrease layer",
                ["Layer_PlusSide__tip"] = "Increase layer",
                ["Layer_MinusSide__tip"] = "Decrease layer",
                ["Tool_Select"] = "Select",
                ["Tool_Line"] = "Line",
                ["Tool_1x1"] = "1×1",
                ["Tool_2x1"] = "2×1",
                ["Tool_2x2"] = "2×2",
                ["Tool_Brush"] = "Brush 3×3",
                ["Tool_Eraser"] = "Eraser",
                ["Tool_FillRect"] = "Fill Rect",
                ["Tool_WallRect"] = "Wall Rect",
                ["Tool_FillCircle"] = "Fill Circle",
                ["Tool_WallCircle"] = "Wall Circle",
                ["Tool_FillEllipse"] = "Fill Ellipse",
                ["Tool_WallEllipse"] = "Wall Ellipse",
                ["Tool_Zone"] = "Zone",
                ["Edit_Undo"] = "Undo",
                ["Edit_Redo"] = "Redo",
                ["Edit_Copy"] = "Copy",
                ["Edit_Paste"] = "Paste",
                ["Edit_Apply"] = "Apply",
                ["Edit_Delete"] = "Delete",
                ["Edit_NewScene"] = "New scene",
                ["Rot_Plus"] = "Rotate +",
                ["Rot_Minus"] = "Rotate −",
                ["Axis_X"] = "Axis X",
                ["Axis_Y"] = "Axis Y",
                ["Axis_Z"] = "Axis Z",
                ["Lbl_Scene"] = "Scene",
                ["Lbl_SizeBlocks"] = "Size (blocks) W×H",
                ["Btn_ApplySize"] = "Apply size",
                ["Lbl_Block"] = "Block",
                ["Lbl_State"] = "State",
                ["Lbl_Color"] = "Color",
                ["Lbl_Hex"] = "hex",
                ["Snap_Grid"] = "Snap to grid",
                ["File_SaveLegacy"] = "Save Legacy",
                ["File_SaveCompact"] = "Save Compact",
                ["File_SaveWrapper"] = "Save Wrapper",
                ["File_LoadLegacy"] = "Load Legacy",
                ["Lbl_Layer"] = "Layer:",
                ["Layer_Plus"] = "Layer +",
                ["Layer_Plus__tip"] = "Next layer (0–255)",
                ["Layer_Minus"] = "Layer −",
                ["Layer_Minus__tip"] = "Previous layer",
                ["Layer_PlusSide"] = "Layer +",
                ["Layer_MinusSide"] = "Layer −",
                ["Zoom_Out"] = "Zoom −",
                ["Zoom_Out__tip"] = "Zoom out",
                ["Zoom_In"] = "Zoom +",
                ["Zoom_In__tip"] = "Zoom in",
                ["Grp_Layers"] = "Layers",
                ["Layer_Shortcuts"] = "Shortcuts: Ctrl+Z / Ctrl+Y, Ctrl+C / Ctrl+V, arrows (selection), Del.",
                ["Layer_HintHelp"] = "Layer hint (like Java): from layer 1, an image shows on empty cells of the active layer that align with a block on the layer below. Optional files: layer_block.png, layer_hint.png or stack_marker.png next to the exe. Layer 0: no indicator.",
                ["Grp_Palette"] = "Block palette",
            }
        };

        public static string T(string key)
        {
            if (!Dictionaries.TryGetValue(CurrentLanguage, out var dict))
                dict = Dictionaries[DefaultLanguage];
            return dict.TryGetValue(key, out var s) ? s : key;
        }

        public static void SetLanguage(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) code = DefaultLanguage;
            code = code.Trim().ToLowerInvariant();
            if (!Dictionaries.ContainsKey(code)) code = DefaultLanguage;
            CurrentLanguage = code;
            SavePrefs();
            LanguageChanged?.Invoke();
        }

        public static void LoadPrefs()
        {
            try
            {
                string path = PrefsPath;
                if (!File.Exists(path)) return;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("language", out var lang))
                {
                    string c = lang.GetString() ?? DefaultLanguage;
                    if (Dictionaries.ContainsKey(c))
                        CurrentLanguage = c;
                }
            }
            catch { /* ignore */ }
        }

        public static void SavePrefs()
        {
            try
            {
                string dir = Path.GetDirectoryName(PrefsPath)!;
                Directory.CreateDirectory(dir);
                var o = new { language = CurrentLanguage };
                File.WriteAllText(PrefsPath, JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore */ }
        }

        private static string PrefsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MagicalChalkStudio",
            "settings.json");

        private const string LocPrefix = "Loc:";
        private const string LocTipPrefix = "LocTip:";

        /// <summary>Applique les textes aux contrôles dont le <see cref="FrameworkElement.Tag"/> vaut <c>Loc:clé</c>.</summary>
        public static void ApplyLocTags(DependencyObject root)
        {
            Walk(root, fe =>
            {
                if (fe.Tag is string tagTip && tagTip.StartsWith(LocTipPrefix, StringComparison.Ordinal))
                {
                    string tk = tagTip[LocTipPrefix.Length..];
                    string tv = T(tk);
                    switch (fe)
                    {
                        case WpfTextBox tx: tx.ToolTip = tv; break;
                        case WpfButton b: b.ToolTip = tv; break;
                    }
                    return;
                }
                if (fe.Tag is not string tag || !tag.StartsWith(LocPrefix, StringComparison.Ordinal)) return;
                string key = tag[LocPrefix.Length..];
                string val = T(key);
                switch (fe)
                {
                    case WpfButton b: b.Content = val; break;
                    case WpfCheckBox cb: cb.Content = val; break;
                    case WpfGroupBox gb: gb.Header = val; break;
                    case WpfTextBlock tb: tb.Text = val; break;
                }
                string tipKey = key + "__tip";
                string tip = T(tipKey);
                if (tip != tipKey)
                {
                    switch (fe)
                    {
                        case WpfButton b: b.ToolTip = tip; break;
                        case WpfCheckBox cb: cb.ToolTip = tip; break;
                        case WpfTextBox tx: tx.ToolTip = tip; break;
                        case WpfTextBlock tb: tb.ToolTip = tip; break;
                    }
                }
            });
        }

        private static void Walk(DependencyObject d, Action<FrameworkElement> onFe)
        {
            if (d is FrameworkElement fe)
                onFe(fe);
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++)
                Walk(VisualTreeHelper.GetChild(d, i), onFe);
        }
    }
}

