package com.magicalchalkstudion.magicalchalkstudio;

import javafx.animation.PauseTransition;
import javafx.application.Platform;
import javafx.fxml.FXML;
import javafx.geometry.BoundingBox;
import javafx.geometry.Bounds;
import javafx.geometry.Point2D;
import javafx.util.Duration;
import javafx.scene.Node;
import javafx.scene.control.*;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.input.KeyCode;
import javafx.scene.input.KeyEvent;
import javafx.scene.input.MouseButton;
import javafx.scene.Group;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Pane;
import javafx.scene.paint.Color;
import javafx.scene.shape.*;
import javafx.stage.FileChooser;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.*;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
import javafx.scene.text.Font;
import javafx.scene.text.FontWeight;
import javafx.scene.text.Text;
import javafx.stage.Window;
public class HelloController {

    private enum Tool {
        SELECT,
        LINE,
        BLOCK_1X1,
        BLOCK_2X1,
        BLOCK_2X2,
        BRUSH_3X3,
        ERASER_1X1,
        FILL_RECT,
        WALL_RECT,
        FILL_CIRCLE,
        WALL_CIRCLE,
        FILL_ELLIPSE,
        WALL_ELLIPSE,
        ZONE_SELECT
    }

    private enum ResizeHandleType {
        TOP_LEFT,
        TOP_RIGHT,
        BOTTOM_LEFT,
        BOTTOM_RIGHT
    }

    private static class RectData {
        double x, y, width, height;
        String blockId;
        String state;
        Color color;
        int layer;

        RectData(double x, double y, double width, double height, String blockId, String state, Color color, int layer) {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.blockId = blockId;
            this.state = state;
            this.color = color;
            this.layer = layer;
        }
    }

    private static class EditorSnapshot {
        List<RectData> rects;
        int currentLayer;

        EditorSnapshot(List<RectData> rects, int currentLayer) {
            this.rects = rects;
            this.currentLayer = currentLayer;
        }
    }

    private static final String USER_DATA_BLOCK_ID = "blockId";
    private static final String USER_DATA_LAYER = "layer";
    private static final String USER_DATA_STATE = "blockStateString";
    private static final String USER_DATA_DIRECTION_TEXT = "directionText";
    private static final double DEFAULT_SCENE_WIDTH = 50_000_000;
    private static final double DEFAULT_SCENE_HEIGHT = 50_000_000;
    private static final double GRID_SIZE = 50;
    /** Zoom minimum ~1 % (Ctrl + molette). */
    private static final double MIN_ZOOM = 0.01;
    /** Zoom maximum 100 % (1.0 = taille réelle du canevas). */
    private static final double MAX_ZOOM = 1.0;
    private static final double ZOOM_FACTOR = 1.02;
    private static final double HANDLE_SIZE = 10;

    @FXML private ScrollPane scrollPane;
    @FXML private Pane canvasPane;
    @FXML private CheckBox snapCheckBox;
    @FXML private TextField xField;
    @FXML private TextField yField;
    @FXML private TextField widthField;
    @FXML private TextField heightField;
    @FXML private TextField blockIdField;
    @FXML private TextField stateField;
    @FXML private TextField sceneNameField;
    @FXML private ColorPicker colorPicker;
    @FXML private Label welcomeText;
    @FXML private Label statusLabel;
    @FXML private Label layerLabel;
    @FXML private Label zoomLabel;
    @FXML private Slider zoomSlider;
    @FXML private Label directionPreviewLabel;
    @FXML private ListView<String> blockList;
    @FXML private TextField searchField;
    @FXML private TextField canvasWidthField;
    @FXML private TextField canvasHeightField;
    private Shape previewShape;
    private Text previewInfoText;
    private double scale = 1.0;
    private boolean suppressZoomSliderEvents;
    private boolean zoomSliderListenerAttached;
    private int currentLayer = 0;
    private Tool currentTool = Tool.SELECT;

    private final List<Rectangle> sceneObjects = new ArrayList<>();
    private final LinkedHashSet<Rectangle> selectedRects = new LinkedHashSet<>();
    private Rectangle selectedRect;

    private final List<String> blockPalette = new ArrayList<>();
    private String selectedBlockId = "minecraft:stone";
    private String currentSceneName = "structure";

    private Rectangle handleTopLeft;
    private Rectangle handleTopRight;
    private Rectangle handleBottomLeft;
    private Rectangle handleBottomRight;
    private final List<Rectangle> resizeHandles = new ArrayList<>();

    private Point2D dragStartGrid;
    private Rectangle previewRect;
    private Rectangle zoneSelectionRect;
    private Rectangle ghostPreview;

    private final List<RectData> clipboard = new ArrayList<>();
    private final Deque<EditorSnapshot> undoStack = new ArrayDeque<>();
    private final Deque<EditorSnapshot> redoStack = new ArrayDeque<>();

    private Image layerHintImage;
    private final List<ImageView> layerHintViews = new ArrayList<>();

    private double sceneWidth = DEFAULT_SCENE_WIDTH;
    private double sceneHeight = DEFAULT_SCENE_HEIGHT;
    private Group gridGroup;
    private PauseTransition gridRedrawDebounce;

    @FXML
    public void initialize() {
        // Allow startup with a minimal FXML (template view) without crashing.
        if (canvasPane == null || scrollPane == null || colorPicker == null
                || blockIdField == null || sceneNameField == null || directionPreviewLabel == null) {
            if (welcomeText != null) {
                welcomeText.setText("Magical Chalk Studio");
            }
            return;
        }

        setupScene();
        hookViewportGridListeners();
        scheduleGridRedraw();
        createResizeHandles();
        //createPreviewRect();
        createPreviewShape();
        createZoneSelectionRect();
        createGhostPreview();
        createPreviewInfoText();
        loadLayerHintImage();

        setupZoom();
        scale = clamp(scale, MIN_ZOOM, MAX_ZOOM);
        canvasPane.setScaleX(scale);
        canvasPane.setScaleY(scale);
        configureZoomSlider();
        setupCanvasInteractions();
        setupKeyboardHandling();
        setupShortcuts();

        colorPicker.setValue(Color.web("#bfbfbf"));

        loadBlockPalette();
        setupBlockPaletteUI();

        blockIdField.setText(selectedBlockId);
        sceneNameField.setText(currentSceneName);
        directionPreviewLabel.setText("-");

        if (canvasWidthField != null) {
            canvasWidthField.setText(String.format(Locale.US, "%.0f", sceneWidth));
        }
        if (canvasHeightField != null) {
            canvasHeightField.setText(String.format(Locale.US, "%.0f", sceneHeight));
        }

        sceneNameField.textProperty().addListener((obs, oldVal, newVal) ->
                currentSceneName = sanitizeSceneName(newVal)
        );

        updateLayerZoomLabels();
        refreshLayerVisibility();
        updateStatus("outil sélection");
        pushUndoSnapshot();

        Platform.runLater(this::scheduleGridRedraw);
        // Après le 1er pulse de layout : ré-applique min/max du Slider (évite un FXML / jar ancien à max 0,2).
        Platform.runLater(() -> Platform.runLater(this::enforceZoomAfterFirstLayout));
    }

    @FXML
    private void onHelloButtonClick() {
        if (welcomeText != null) {
            welcomeText.setText("Magical Chalk Studio prêt");
        } else {
            updateStatus("application prête");
        }
    }

    private void setupScene() {
        if (gridGroup == null) {
            gridGroup = new Group();
            canvasPane.getChildren().add(0, gridGroup);
        }
        canvasPane.setPrefSize(sceneWidth, sceneHeight);
        canvasPane.setMinSize(sceneWidth, sceneHeight);
        canvasPane.setMaxSize(Double.MAX_VALUE, Double.MAX_VALUE);
        canvasPane.setStyle("-fx-background-color: #2b2b2b;");
        canvasPane.setFocusTraversable(true);
        scrollPane.setPannable(true);
        // fitToWidth/Height=true réduit le contenu à la fenêtre : plus de scroll ni grille correcte sur grandes tailles.
        scrollPane.setFitToWidth(false);
        scrollPane.setFitToHeight(false);
    }

    private void hookViewportGridListeners() {
        javafx.beans.InvalidationListener l = obs -> scheduleGridRedraw();
        scrollPane.viewportBoundsProperty().addListener(l);
        scrollPane.hvalueProperty().addListener(l);
        scrollPane.vvalueProperty().addListener(l);
        canvasPane.scaleXProperty().addListener(l);
        canvasPane.scaleYProperty().addListener(l);
        scrollPane.widthProperty().addListener(l);
        scrollPane.heightProperty().addListener(l);
        scrollPane.sceneProperty().addListener((obs, oldScene, newScene) -> {
            if (newScene != null) {
                Platform.runLater(this::scheduleGridRedraw);
            }
        });
    }

    private void scheduleGridRedraw() {
        if (gridGroup == null || scrollPane == null) {
            return;
        }
        if (gridRedrawDebounce == null) {
            gridRedrawDebounce = new PauseTransition(Duration.millis(45));
            gridRedrawDebounce.setOnFinished(e -> refreshViewportGrid());
        }
        gridRedrawDebounce.stop();
        gridRedrawDebounce.playFromStart();
    }

    /** Au-delà de cette taille, on privilégie layoutX/layoutY pour le scroll (plus fiable que hvalue seul). */
    private static final double SCENE_USE_LAYOUT_SCROLL_MIN = 1_000_000;

    private static double viewportWorldWidth(Bounds vp, double appliedScale) {
        return vp.getWidth() / Math.max(1e-9, appliedScale);
    }

    private static double viewportWorldHeight(Bounds vp, double appliedScale) {
        return vp.getHeight() / Math.max(1e-9, appliedScale);
    }

    private double visibleOriginXWorld(Bounds vp, double appliedScale) {
        double visW = viewportWorldWidth(vp, appliedScale);
        double maxOx = Math.max(0, sceneWidth - visW);
        if (maxOx <= 0) {
            return 0;
        }
        double fromHvalue = clamp(scrollPane.getHvalue() * maxOx, 0, maxOx);
        double fromLayout = clamp(-canvasPane.getLayoutX(), 0, maxOx);
        boolean huge = sceneWidth >= SCENE_USE_LAYOUT_SCROLL_MIN || sceneHeight >= SCENE_USE_LAYOUT_SCROLL_MIN;
        if (huge) {
            if (Math.abs(canvasPane.getLayoutX()) > 1e-3) {
                return fromLayout;
            }
            return fromHvalue;
        }
        if (Math.abs(canvasPane.getLayoutX()) > 1e-4) {
            return fromLayout;
        }
        return fromHvalue;
    }

    private double visibleOriginYWorld(Bounds vp, double appliedScale) {
        double visH = viewportWorldHeight(vp, appliedScale);
        double maxOy = Math.max(0, sceneHeight - visH);
        if (maxOy <= 0) {
            return 0;
        }
        double fromHvalue = clamp(scrollPane.getVvalue() * maxOy, 0, maxOy);
        double fromLayout = clamp(-canvasPane.getLayoutY(), 0, maxOy);
        boolean huge = sceneWidth >= SCENE_USE_LAYOUT_SCROLL_MIN || sceneHeight >= SCENE_USE_LAYOUT_SCROLL_MIN;
        if (huge) {
            if (Math.abs(canvasPane.getLayoutY()) > 1e-3) {
                return fromLayout;
            }
            return fromHvalue;
        }
        if (Math.abs(canvasPane.getLayoutY()) > 1e-4) {
            return fromLayout;
        }
        return fromHvalue;
    }

    /**
     * Taille utile du viewport ScrollPane (avec secours si le layout n'est pas encore prêt).
     * @return {@code [largeur, hauteur, minX, minY]} dans l'espace local du ScrollPane
     */
    private double[] effectiveViewportDimensions() {
        Bounds vp = scrollPane.getViewportBounds();
        double vpW = vp.getWidth();
        double vpH = vp.getHeight();
        if (vpW <= 4 || vpH <= 4) {
            double lw = scrollPane.getLayoutBounds().getWidth();
            double lh = scrollPane.getLayoutBounds().getHeight();
            if (lw > 8) {
                vpW = Math.max(vpW, lw - 6);
            }
            if (lh > 8) {
                vpH = Math.max(vpH, lh - 6);
            }
        }
        if (vpW <= 1 || vpH <= 1) {
            vpW = Math.max(scrollPane.getWidth() * 0.95, 400);
            vpH = Math.max(scrollPane.getHeight() * 0.95, 300);
        }
        return new double[] { vpW, vpH, vp.getMinX(), vp.getMinY() };
    }

    /**
     * Rectangle visible du canevas en coordonnées monde (locales du {@code canvasPane}),
     * aligné sur le scroll et le zoom via la chaîne de transformations (plus fiable que hvalue seul).
     */
    private Bounds computeVisibleWorldBounds() {
        double[] eff = effectiveViewportDimensions();
        double vpW = eff[0];
        double vpH = eff[1];
        double vpMinX = eff[2];
        double vpMinY = eff[3];
        if (vpW <= 1 || vpH <= 1 || canvasPane == null) {
            return new BoundingBox(0, 0, 1, 1);
        }
        double sx = Math.max(1e-6, canvasPane.getScaleX());
        double sy = Math.max(1e-6, canvasPane.getScaleY());
        try {
            Point2D c0 = canvasPane.sceneToLocal(scrollPane.localToScene(vpMinX, vpMinY));
            Point2D c1 = canvasPane.sceneToLocal(scrollPane.localToScene(vpMinX + vpW, vpMinY));
            Point2D c2 = canvasPane.sceneToLocal(scrollPane.localToScene(vpMinX, vpMinY + vpH));
            Point2D c3 = canvasPane.sceneToLocal(scrollPane.localToScene(vpMinX + vpW, vpMinY + vpH));
            double minX = Math.min(Math.min(c0.getX(), c1.getX()), Math.min(c2.getX(), c3.getX()));
            double maxX = Math.max(Math.max(c0.getX(), c1.getX()), Math.max(c2.getX(), c3.getX()));
            double minY = Math.min(Math.min(c0.getY(), c1.getY()), Math.min(c2.getY(), c3.getY()));
            double maxY = Math.max(Math.max(c0.getY(), c1.getY()), Math.max(c2.getY(), c3.getY()));
            if (!Double.isFinite(minX) || !Double.isFinite(maxX) || !Double.isFinite(minY) || !Double.isFinite(maxY)) {
                Bounds vpBox = new BoundingBox(vpMinX, vpMinY, vpW, vpH);
                double minOx = visibleOriginXWorld(vpBox, sx);
                double minOy = visibleOriginYWorld(vpBox, sy);
                return new BoundingBox(minOx, minOy, vpW / sx, vpH / sy);
            }
            minX = clamp(minX, 0, sceneWidth);
            maxX = clamp(maxX, 0, sceneWidth);
            minY = clamp(minY, 0, sceneHeight);
            maxY = clamp(maxY, 0, sceneHeight);
            double w = Math.max(1e-6, maxX - minX);
            double h = Math.max(1e-6, maxY - minY);
            return new BoundingBox(minX, minY, w, h);
        } catch (Exception e) {
            Bounds vpBox = new BoundingBox(vpMinX, vpMinY, vpW, vpH);
            double minOx = visibleOriginXWorld(vpBox, sx);
            double minOy = visibleOriginYWorld(vpBox, sy);
            return new BoundingBox(minOx, minOy, vpW / sx, vpH / sy);
        }
    }

    /**
     * Grille dessinée uniquement sur la zone visible (scroll + zoom), pour rester lisible
     * même sur des créations très grandes (ex. 50M × 50M).
     */
    private void refreshViewportGrid() {
        if (gridGroup == null || scrollPane == null) {
            return;
        }
        gridGroup.getChildren().clear();

        double[] eff = effectiveViewportDimensions();
        double vpW = eff[0];
        double vpH = eff[1];
        if (vpW <= 1 || vpH <= 1) {
            return;
        }

        double worldW = sceneWidth;
        double worldH = sceneHeight;

        Bounds visibleWorld = computeVisibleWorldBounds();
        double minOx = visibleWorld.getMinX();
        double minOy = visibleWorld.getMinY();
        double visW = visibleWorld.getWidth();
        double visH = visibleWorld.getHeight();

        double padX = Math.max(GRID_SIZE, visW * 0.1);
        double padY = Math.max(GRID_SIZE, visH * 0.1);

        double startX = clamp(minOx - padX, 0, worldW);
        double endX = clamp(minOx + visW + padX, 0, worldW);
        double startY = clamp(minOy - padY, 0, worldH);
        double endY = clamp(minOy + visH + padY, 0, worldH);

        double spanX = endX - startX;
        double spanY = endY - startY;
        int maxMinorLines = 520;
        double stepX = spanX / GRID_SIZE <= maxMinorLines
                ? GRID_SIZE
                : Math.max(GRID_SIZE, Math.round((spanX / 48.0) / GRID_SIZE) * GRID_SIZE);
        double stepY = spanY / GRID_SIZE <= maxMinorLines
                ? GRID_SIZE
                : Math.max(GRID_SIZE, Math.round((spanY / 48.0) / GRID_SIZE) * GRID_SIZE);

        final int maxGridLines = 800;
        if (spanX > 0 && spanX / stepX > maxGridLines) {
            stepX = spanX / (maxGridLines - 1);
            stepX = Math.max(GRID_SIZE, Math.round(stepX / GRID_SIZE) * GRID_SIZE);
        }
        if (spanY > 0 && spanY / stepY > maxGridLines) {
            stepY = spanY / (maxGridLines - 1);
            stepY = Math.max(GRID_SIZE, Math.round(stepY / GRID_SIZE) * GRID_SIZE);
        }

        int xi = 0;
        double x = Math.floor(startX / stepX) * stepX;
        if (x < startX - 1e-6) {
            x += stepX;
        }
        for (; x <= endX + 1e-6; x += stepX) {
            Line line = new Line(x, startY, x, endY);
            line.setStroke(xi % 5 == 0 ? Color.web("#d0d0d0") : Color.web("#b0b0b0"));
            line.setStrokeWidth(xi % 5 == 0 ? 1.35 : 1.0);
            line.setMouseTransparent(true);
            line.setSmooth(false);
            gridGroup.getChildren().add(line);
            xi++;
        }

        int yi = 0;
        double y = Math.floor(startY / stepY) * stepY;
        if (y < startY - 1e-6) {
            y += stepY;
        }
        for (; y <= endY + 1e-6; y += stepY) {
            Line line = new Line(startX, y, endX, y);
            line.setStroke(yi % 5 == 0 ? Color.web("#d0d0d0") : Color.web("#b0b0b0"));
            line.setStrokeWidth(yi % 5 == 0 ? 1.35 : 1.0);
            line.setMouseTransparent(true);
            line.setSmooth(false);
            gridGroup.getChildren().add(line);
            yi++;
        }

        gridGroup.toBack();
    }

    private void createPreviewShape() {
        previewShape = new Rectangle();
        previewShape.setVisible(false);
        previewShape.setMouseTransparent(true);
        canvasPane.getChildren().add(previewShape);
    }

    private void createPreviewInfoText() {
        previewInfoText = new Text();
        previewInfoText.setMouseTransparent(true);
        previewInfoText.setManaged(false);
        previewInfoText.setFill(Color.web("#5EDCFF"));
        previewInfoText.setStroke(Color.BLACK);
        previewInfoText.setStrokeWidth(0.7);
        previewInfoText.setFont(Font.font("Arial", FontWeight.BOLD, 14));
        previewInfoText.setVisible(false);
        canvasPane.getChildren().add(previewInfoText);
    }

    private void applyPreviewStyle(Shape shape, boolean filled) {
        shape.setStroke(Color.CYAN);
        shape.setStrokeWidth(1.5);
        shape.getStrokeDashArray().clear();
        shape.getStrokeDashArray().addAll(6.0, 6.0);
        shape.setMouseTransparent(true);
        shape.setVisible(true);

        if (filled) {
            shape.setFill(Color.color(0.2, 0.8, 1.0, 0.18));
        } else {
            shape.setFill(Color.TRANSPARENT);
        }
    }
    private void showPreviewShape(Point2D a, Point2D b) {
        updatePreviewShape(a, b);
    }
    private void updatePreviewShape(Point2D a, Point2D b) {
        double minX = Math.min(a.getX(), b.getX());
        double maxX = Math.max(a.getX(), b.getX());
        double minY = Math.min(a.getY(), b.getY());
        double maxY = Math.max(a.getY(), b.getY());

        Shape newShape;

        switch (currentTool) {
            case FILL_CIRCLE, WALL_CIRCLE -> {
                double centerX = a.getX();
                double centerY = a.getY();
                double radius = Math.max(GRID_SIZE, Math.hypot(b.getX() - a.getX(), b.getY() - a.getY()));
                Circle circle = new Circle(centerX, centerY, radius);
                applyPreviewStyle(circle, currentTool == Tool.FILL_CIRCLE);
                newShape = circle;
            }

            case FILL_ELLIPSE, WALL_ELLIPSE -> {
                double centerX = (minX + maxX) / 2.0;
                double centerY = (minY + maxY) / 2.0;
                double radiusX = Math.max(GRID_SIZE / 2.0, (maxX - minX) / 2.0);
                double radiusY = Math.max(GRID_SIZE / 2.0, (maxY - minY) / 2.0);
                Ellipse ellipse = new Ellipse(centerX, centerY, radiusX, radiusY);
                applyPreviewStyle(ellipse, currentTool == Tool.FILL_ELLIPSE);
                newShape = ellipse;
            }

            case LINE -> {
                Line line = new Line(a.getX(), a.getY(), b.getX(), b.getY());
                applyPreviewStyle(line, false);
                newShape = line;
            }

            default -> {
                Rectangle rect = new Rectangle(
                        minX,
                        minY,
                        Math.abs(a.getX() - b.getX()) + GRID_SIZE,
                        Math.abs(a.getY() - b.getY()) + GRID_SIZE
                );
                boolean filled = currentTool == Tool.FILL_RECT || currentTool == Tool.ZONE_SELECT;
                applyPreviewStyle(rect, filled);
                if (currentTool == Tool.ZONE_SELECT) {
                    rect.setFill(Color.color(1.0, 1.0, 0.0, 0.15));
                    rect.setStroke(Color.YELLOW);
                }
                newShape = rect;
            }
        }

        canvasPane.getChildren().remove(previewShape);
        previewShape = newShape;
        canvasPane.getChildren().add(previewShape);
        previewShape.toFront();

        if (previewInfoText != null) {
            previewInfoText.toFront();
        }
    }

    private void updatePreviewInfo(Point2D a, Point2D b) {
        if (previewInfoText == null) return;

        double minX = Math.min(a.getX(), b.getX());
        double maxX = Math.max(a.getX(), b.getX());
        double minY = Math.min(a.getY(), b.getY());
        double maxY = Math.max(a.getY(), b.getY());

        int widthBlocks = (int) ((Math.abs(a.getX() - b.getX()) / GRID_SIZE) + 1);
        int heightBlocks = (int) ((Math.abs(a.getY() - b.getY()) / GRID_SIZE) + 1);

        String text;

        switch (currentTool) {
            case FILL_CIRCLE, WALL_CIRCLE -> {
                double radiusPx = Math.max(GRID_SIZE, Math.hypot(b.getX() - a.getX(), b.getY() - a.getY()));
                int radiusBlocks = Math.max(1, (int) Math.round(radiusPx / GRID_SIZE));
                int diameterBlocks = radiusBlocks * 2;
                text = "Rayon: " + radiusBlocks + " | Diamètre: " + diameterBlocks;
            }

            case FILL_ELLIPSE, WALL_ELLIPSE -> {
                text = "Largeur: " + widthBlocks + " | Hauteur: " + heightBlocks;
            }

            case LINE -> {
                double dx = b.getX() - a.getX();
                double dy = b.getY() - a.getY();
                int lengthBlocks = Math.max(1, (int) Math.round(Math.hypot(dx, dy) / GRID_SIZE));
                text = "Longueur: " + lengthBlocks;
            }

            case FILL_RECT, WALL_RECT, ZONE_SELECT -> {
                text = "Largeur: " + widthBlocks + " | Hauteur: " + heightBlocks;
            }

            default -> {
                previewInfoText.setVisible(false);
                return;
            }
        }

        previewInfoText.setText(text);
        previewInfoText.applyCss();

        double textX = maxX + 10;
        double textY = minY - 10;

        if (textY < 20) {
            textY = maxY + 20;
        }

        previewInfoText.setX(textX);
        previewInfoText.setY(textY);
        previewInfoText.toFront();
        previewInfoText.setVisible(true);
    }


    private void loadLayerHintImage() {
        try {
            layerHintImage = new Image(
                    Objects.requireNonNull(
                            HelloController.class.getResourceAsStream("layer_hint.png")
                    )
            );
        } catch (Exception e) {
            layerHintImage = null;
        }
    }

    private void loadBlockPalette() {
        try {
            Path path = Path.of("blocks.txt");
            if (!Files.exists(path)) {
                updateStatus("blocks.txt introuvable");
                return;
            }

            List<String> lines = Files.readAllLines(path, StandardCharsets.UTF_8);
            blockPalette.clear();

            for (String line : lines) {
                String trimmed = line.trim();
                if (trimmed.isEmpty()) continue;
                if (trimmed.startsWith("#")) continue;
                blockPalette.add(trimmed);
            }

            if (!blockPalette.isEmpty()) {
                selectedBlockId = blockPalette.get(0);
            }
        } catch (Exception e) {
            updateStatus("erreur lecture blocks.txt");
        }
    }

    private void setupBlockPaletteUI() {
        blockList.getItems().clear();
        blockList.getItems().addAll(blockPalette);

        blockList.setCellFactory(listView -> new ListCell<>() {
            private final Rectangle colorBox = new Rectangle(14, 14);
            private final Label textLabel = new Label();
            private final HBox content = new HBox(8, colorBox, textLabel);

            @Override
            protected void updateItem(String item, boolean empty) {
                super.updateItem(item, empty);

                if (empty || item == null) {
                    setText(null);
                    setGraphic(null);
                    return;
                }

                colorBox.setFill(getBlockPreviewColor(item));
                colorBox.setStroke(Color.web("#bbbbbb"));
                textLabel.setText(item);
                textLabel.setTextFill(Color.web("#dddddd"));
                setGraphic(content);
            }
        });

        if (!blockPalette.isEmpty()) {
            blockList.getSelectionModel().select(selectedBlockId);
        }

        blockList.getSelectionModel().selectedItemProperty().addListener((obs, oldVal, newVal) -> {
            if (newVal != null) {
                selectedBlockId = newVal;
                if (selectedRect != null) {
                    blockIdField.setText(selectedBlockId);
                }
                updateStatus("bloc sélectionné : " + selectedBlockId);
            }
        });

        searchField.textProperty().addListener((obs, oldVal, newVal) -> filterBlockPalette(newVal));
    }

    private void filterBlockPalette(String query) {
        String q = query == null ? "" : query.toLowerCase(Locale.ROOT).trim();
        blockList.getItems().clear();

        for (String block : blockPalette) {
            if (q.isEmpty() || block.toLowerCase(Locale.ROOT).contains(q)) {
                blockList.getItems().add(block);
            }
        }
    }

    private Color getBlockPreviewColor(String blockId) {
        String id = blockId.toLowerCase(Locale.ROOT);

        if (id.contains("grass")) return Color.web("#5ca04a");
        if (id.contains("stone")) return Color.web("#808080");
        if (id.contains("deepslate")) return Color.web("#4d4d57");
        if (id.contains("dirt")) return Color.web("#7a5937");
        if (id.contains("sand")) return Color.web("#d8c07a");
        if (id.contains("water")) return Color.web("#4d79d8");
        if (id.contains("lava")) return Color.web("#d85a2f");
        if (id.contains("log") || id.contains("planks") || id.contains("wood")) return Color.web("#9c6b3e");
        if (id.contains("leaves")) return Color.web("#4b8a43");
        if (id.contains("glass")) return Color.web("#98c7d8");
        if (id.contains("diamond")) return Color.web("#57d6d6");
        if (id.contains("gold")) return Color.web("#d8b840");
        if (id.contains("iron")) return Color.web("#c2c2c2");
        if (id.contains("redstone")) return Color.web("#b53a3a");
        if (id.contains("emerald")) return Color.web("#3db55f");
        if (id.contains("amethyst")) return Color.web("#9c73d8");
        if (id.contains("obsidian")) return Color.web("#3d3155");
        return Color.web("#bfbfbf");
    }

    private void bringDirectionTextsToFront() {
        for (Rectangle rect : sceneObjects) {
            Object o = rect.getProperties().get(USER_DATA_DIRECTION_TEXT);
            if (o instanceof Text t) {
                t.toFront();
            }
        }
    }

    private void bringEditingChromeToFront() {
        for (Rectangle handle : resizeHandles) {
            handle.toFront();
        }
        if (ghostPreview != null) {
            ghostPreview.toFront();
        }
        if (previewShape != null) {
            previewShape.toFront();
        }
        if (zoneSelectionRect != null) {
            zoneSelectionRect.toFront();
        }
        if (previewInfoText != null) {
            previewInfoText.toFront();
        }
    }

    /** Recrée les glyphes direction / rotation pour les blocs visibles. */
    private void refreshDirectionOverlays() {
        for (Rectangle rect : sceneObjects) {
            removeDirectionText(rect);
            if (rect.isVisible()) {
                updateRectVisualDirection(rect);
            }
        }
        bringDirectionTextsToFront();
    }

    private void bringSceneNodesToFront() {
        for (Rectangle rect : sceneObjects) {
            rect.toFront();
        }
        refreshDirectionOverlays();
        bringEditingChromeToFront();
    }

    private void createPreviewRect() {
        previewRect = new Rectangle();
        previewRect.setFill(Color.color(0.2, 0.8, 1.0, 0.18));
        previewRect.setStroke(Color.CYAN);
        previewRect.setStrokeWidth(1.5);
        previewRect.getStrokeDashArray().addAll(6.0, 6.0);
        previewRect.setVisible(false);
        previewRect.setMouseTransparent(true);
        canvasPane.getChildren().add(previewRect);
    }

    private void createZoneSelectionRect() {
        zoneSelectionRect = new Rectangle();
        zoneSelectionRect.setFill(Color.color(1.0, 1.0, 0.0, 0.15));
        zoneSelectionRect.setStroke(Color.YELLOW);
        zoneSelectionRect.setStrokeWidth(1.5);
        zoneSelectionRect.getStrokeDashArray().addAll(8.0, 6.0);
        zoneSelectionRect.setVisible(false);
        zoneSelectionRect.setMouseTransparent(true);
        canvasPane.getChildren().add(zoneSelectionRect);
    }

    private void createGhostPreview() {
        ghostPreview = new Rectangle();
        ghostPreview.setFill(Color.color(0.2, 0.9, 1.0, 0.18));
        ghostPreview.setStroke(Color.CYAN);
        ghostPreview.setStrokeWidth(1.2);
        ghostPreview.getStrokeDashArray().addAll(6.0, 6.0);
        ghostPreview.setVisible(false);
        ghostPreview.setMouseTransparent(true);
        canvasPane.getChildren().add(ghostPreview);

        canvasPane.setOnMouseMoved(e -> {
            if (dragStartGrid != null) {
                ghostPreview.setVisible(false);
                return;
            }

            if (currentTool == Tool.SELECT || currentTool == Tool.ZONE_SELECT) {
                ghostPreview.setVisible(false);
                return;
            }

            Point2D p = toGridPoint(e.getX(), e.getY());

            double w = GRID_SIZE;
            double h = GRID_SIZE;

            if (currentTool == Tool.BLOCK_2X1) {
                w = GRID_SIZE * 2;
            } else if (currentTool == Tool.BLOCK_2X2) {
                w = GRID_SIZE * 2;
                h = GRID_SIZE * 2;
            }

            ghostPreview.setX(p.getX());
            ghostPreview.setY(p.getY());
            ghostPreview.setWidth(w);
            ghostPreview.setHeight(h);
            ghostPreview.toFront();
            ghostPreview.setVisible(true);
        });
    }

    private void createResizeHandles() {
        handleTopLeft = createHandle(ResizeHandleType.TOP_LEFT);
        handleTopRight = createHandle(ResizeHandleType.TOP_RIGHT);
        handleBottomLeft = createHandle(ResizeHandleType.BOTTOM_LEFT);
        handleBottomRight = createHandle(ResizeHandleType.BOTTOM_RIGHT);

        resizeHandles.add(handleTopLeft);
        resizeHandles.add(handleTopRight);
        resizeHandles.add(handleBottomLeft);
        resizeHandles.add(handleBottomRight);

        canvasPane.getChildren().addAll(resizeHandles);
        hideResizeHandles();
    }

    private Rectangle createHandle(ResizeHandleType type) {
        Rectangle handle = new Rectangle(HANDLE_SIZE, HANDLE_SIZE);
        handle.setFill(Color.CYAN);
        handle.setStroke(Color.WHITE);
        handle.setStrokeWidth(1);
        handle.setVisible(false);
        handle.setManaged(false);

        final double[] startRect = new double[4];

        handle.setOnMousePressed(e -> {
            if (selectedRect == null) return;

            startRect[0] = selectedRect.getX();
            startRect[1] = selectedRect.getY();
            startRect[2] = selectedRect.getWidth();
            startRect[3] = selectedRect.getHeight();

            e.consume();
        });

        handle.setOnMouseDragged(e -> {
            if (selectedRect == null) return;

            Point2D p = canvasPane.sceneToLocal(e.getSceneX(), e.getSceneY());
            double mouseX = snap(p.getX());
            double mouseY = snap(p.getY());

            double baseX = startRect[0];
            double baseY = startRect[1];
            double baseW = startRect[2];
            double baseH = startRect[3];

            double right = baseX + baseW;
            double bottom = baseY + baseH;

            double newX = baseX;
            double newY = baseY;
            double newW = baseW;
            double newH = baseH;

            switch (type) {
                case TOP_LEFT -> {
                    newX = clamp(mouseX, 0, right - GRID_SIZE);
                    newY = clamp(mouseY, 0, bottom - GRID_SIZE);
                    newW = right - newX;
                    newH = bottom - newY;
                }
                case TOP_RIGHT -> {
                    double newRight = clamp(mouseX, baseX + GRID_SIZE, sceneWidth);
                    newY = clamp(mouseY, 0, bottom - GRID_SIZE);
                    newW = newRight - baseX;
                    newH = bottom - newY;
                }
                case BOTTOM_LEFT -> {
                    newX = clamp(mouseX, 0, right - GRID_SIZE);
                    double newBottom = clamp(mouseY, baseY + GRID_SIZE, sceneHeight);
                    newW = right - newX;
                    newH = newBottom - baseY;
                }
                case BOTTOM_RIGHT -> {
                    double newRight = clamp(mouseX, baseX + GRID_SIZE, sceneWidth);
                    double newBottom = clamp(mouseY, baseY + GRID_SIZE, sceneHeight);
                    newW = newRight - baseX;
                    newH = newBottom - baseY;
                }
            }

            newW = Math.max(GRID_SIZE, snap(newW));
            newH = Math.max(GRID_SIZE, snap(newH));
            newX = snap(newX);
            newY = snap(newY);

            selectedRect.setX(newX);
            selectedRect.setY(newY);
            selectedRect.setWidth(newW);
            selectedRect.setHeight(newH);

            selectedRect.toFront();
            updatePropertiesPanel(selectedRect);
            updateResizeHandles();

            updateStatus("resize sur grille");
            e.consume();
        });

        return handle;
    }

    private void applyZoomSliderRange() {
        if (zoomSlider == null) {
            return;
        }
        zoomSlider.setMin(MIN_ZOOM);
        zoomSlider.setMax(MAX_ZOOM);
    }

    /** Réaligne curseur + libellé sur les constantes (utile après layout ou si ressources obsolètes). */
    private void enforceZoomAfterFirstLayout() {
        if (canvasPane == null || scrollPane == null) {
            return;
        }
        applyZoomSliderRange();
        double s = clamp(canvasPane.getScaleX(), MIN_ZOOM, MAX_ZOOM);
        if (Math.abs(s - canvasPane.getScaleX()) > 1e-12) {
            canvasPane.setScaleX(s);
            canvasPane.setScaleY(s);
        }
        scale = s;
        syncZoomSliderToScale();
        updateLayerZoomLabels();
        scheduleGridRedraw();
    }

    private void configureZoomSlider() {
        if (zoomSlider == null) {
            return;
        }
        applyZoomSliderRange();
        suppressZoomSliderEvents = true;
        try {
            zoomSlider.setValue(clamp(scale, MIN_ZOOM, MAX_ZOOM));
        } finally {
            suppressZoomSliderEvents = false;
        }
        if (!zoomSliderListenerAttached) {
            zoomSlider.valueProperty().addListener((obs, oldV, newV) -> {
                if (suppressZoomSliderEvents || newV == null) {
                    return;
                }
                applyCanvasZoom(newV.doubleValue(), null);
            });
            zoomSliderListenerAttached = true;
        }
    }

    /**
     * Applique l'échelle du canevas. {@code sceneAnchor} en coordonnées scène : garde ce point fixe (molette) ;
     * {@code null} : garde le centre du viewport (curseur / boutons).
     */
    private void applyCanvasZoom(double newScale, Point2D sceneAnchor) {
        newScale = clamp(newScale, MIN_ZOOM, MAX_ZOOM);
        double oldScale = canvasPane.getScaleX();
        if (Math.abs(newScale - oldScale) < 1e-9) {
            scale = newScale;
            syncZoomSliderToScale();
            updateLayerZoomLabels();
            return;
        }

        Bounds vis = computeVisibleWorldBounds();
        double curOx = vis.getMinX();
        double curOy = vis.getMinY();
        double[] eff = effectiveViewportDimensions();
        double vpW = eff[0];
        double vpH = eff[1];

        if (sceneAnchor != null) {
            Point2D pBefore = canvasPane.sceneToLocal(sceneAnchor.getX(), sceneAnchor.getY());
            canvasPane.setScaleX(newScale);
            canvasPane.setScaleY(newScale);
            Point2D pAfter = canvasPane.sceneToLocal(sceneAnchor.getX(), sceneAnchor.getY());
            double deltaX = pBefore.getX() - pAfter.getX();
            double deltaY = pBefore.getY() - pAfter.getY();

            double maxOxNew = Math.max(0, sceneWidth - vpW / newScale);
            double maxOyNew = Math.max(0, sceneHeight - vpH / newScale);
            double targetOx = curOx + deltaX;
            double targetOy = curOy + deltaY;

            scrollPane.setHvalue(maxOxNew <= 0 ? 0 : clamp(targetOx / maxOxNew, 0, 1));
            scrollPane.setVvalue(maxOyNew <= 0 ? 0 : clamp(targetOy / maxOyNew, 0, 1));
        } else {
            double visWOld = vis.getWidth();
            double visHOld = vis.getHeight();
            double midWx = curOx + visWOld * 0.5;
            double midWy = curOy + visHOld * 0.5;

            canvasPane.setScaleX(newScale);
            canvasPane.setScaleY(newScale);

            double visWNew = vpW / newScale;
            double visHNew = vpH / newScale;
            double maxOxNew = Math.max(0, sceneWidth - vpW / newScale);
            double maxOyNew = Math.max(0, sceneHeight - vpH / newScale);
            double targetOx = midWx - visWNew * 0.5;
            double targetOy = midWy - visHNew * 0.5;

            scrollPane.setHvalue(maxOxNew <= 0 ? 0 : clamp(targetOx / maxOxNew, 0, 1));
            scrollPane.setVvalue(maxOyNew <= 0 ? 0 : clamp(targetOy / maxOyNew, 0, 1));
        }

        scale = newScale;
        syncZoomSliderToScale();
        updateLayerZoomLabels();
        updateResizeHandles();
        scheduleGridRedraw();
    }

    private void syncZoomSliderToScale() {
        if (zoomSlider == null) {
            return;
        }
        applyZoomSliderRange();
        suppressZoomSliderEvents = true;
        try {
            zoomSlider.setValue(clamp(canvasPane.getScaleX(), MIN_ZOOM, MAX_ZOOM));
        } finally {
            suppressZoomSliderEvents = false;
        }
    }

    private void setupZoom() {
        scrollPane.addEventFilter(javafx.scene.input.ScrollEvent.SCROLL, event -> {
            if (!event.isControlDown()) return;

            double oldScale = scale;
            double next = event.getDeltaY() > 0 ? oldScale * ZOOM_FACTOR : oldScale / ZOOM_FACTOR;
            next = clamp(next, MIN_ZOOM, MAX_ZOOM);

            if (Math.abs(next - oldScale) < 0.0001) {
                event.consume();
                return;
            }

            applyCanvasZoom(next, new Point2D(event.getSceneX(), event.getSceneY()));
            event.consume();
        });
    }

    @FXML
    private void onZoomIn() {
        applyCanvasZoom(scale * ZOOM_FACTOR, null);
    }

    @FXML
    private void onZoomOut() {
        applyCanvasZoom(scale / ZOOM_FACTOR, null);
    }

    private void setupCanvasInteractions() {
        canvasPane.setOnMousePressed(event -> {
            canvasPane.requestFocus();

            if (event.getButton() != MouseButton.PRIMARY) return;

            Point2D gridPoint = toGridPoint(event.getX(), event.getY());

            if (event.getTarget() == canvasPane) {
                switch (currentTool) {
                    case SELECT -> {
                        clearSelection();
                        updateStatus("aucune sélection");
                    }
                    case BLOCK_1X1 -> placeBlock(gridPoint.getX(), gridPoint.getY(), 1, 1);
                    case BLOCK_2X1 -> placeBlock(gridPoint.getX(), gridPoint.getY(), 2, 1);
                    case BLOCK_2X2 -> placeBlock(gridPoint.getX(), gridPoint.getY(), 2, 2);
                    case BRUSH_3X3 -> placeBrush3x3(gridPoint.getX(), gridPoint.getY());
                    case ERASER_1X1 -> eraseAt(gridPoint.getX(), gridPoint.getY());
                    case LINE, FILL_RECT, WALL_RECT, FILL_CIRCLE, WALL_CIRCLE, FILL_ELLIPSE, WALL_ELLIPSE, ZONE_SELECT -> {
                        dragStartGrid = gridPoint;
                        showPreviewShape(gridPoint, gridPoint);
                        updatePreviewInfo(gridPoint, gridPoint);

                        if (currentTool == Tool.ZONE_SELECT) {
                            zoneSelectionRect.setVisible(true);
                        }
                    }
                }
                event.consume();
            }
        });

        canvasPane.setOnMouseDragged(event -> {
            if (event.getButton() != MouseButton.PRIMARY) return;

            Point2D gridPoint = toGridPoint(event.getX(), event.getY());

            switch (currentTool) {
                case BRUSH_3X3 -> placeBrush3x3(gridPoint.getX(), gridPoint.getY());
                case ERASER_1X1 -> eraseAt(gridPoint.getX(), gridPoint.getY());
                case LINE, FILL_RECT, WALL_RECT, FILL_CIRCLE, WALL_CIRCLE, FILL_ELLIPSE, WALL_ELLIPSE -> {
                    if (dragStartGrid != null) {
                        showPreviewShape(dragStartGrid, gridPoint);
                        updatePreviewInfo(dragStartGrid, gridPoint);
                    }
                }
                case ZONE_SELECT -> {
                    if (dragStartGrid != null) {
                        showZoneSelection(dragStartGrid, gridPoint);
                        updatePreviewInfo(dragStartGrid, gridPoint);
                    }
                }
                default -> { }
            }
            event.consume();
        });

        canvasPane.setOnMouseReleased(event -> {
            if (event.getButton() != MouseButton.PRIMARY) return;

            Point2D gridPoint = toGridPoint(event.getX(), event.getY());

            if (dragStartGrid != null) {
                switch (currentTool) {
                    case LINE -> placeLine(dragStartGrid, gridPoint);
                    case FILL_RECT -> placeFilledRectangle(dragStartGrid, gridPoint);
                    case WALL_RECT -> placeWallRectangle(dragStartGrid, gridPoint);
                    case FILL_CIRCLE -> placeFilledCircle(dragStartGrid, gridPoint);
                    case WALL_CIRCLE -> placeWallCircle(dragStartGrid, gridPoint);
                    case FILL_ELLIPSE -> placeFilledEllipse(dragStartGrid, gridPoint);
                    case WALL_ELLIPSE -> placeWallEllipse(dragStartGrid, gridPoint);
                    case ZONE_SELECT -> selectZone(dragStartGrid, gridPoint);
                    default -> { }
                }
            }

            dragStartGrid = null;

            if (previewShape != null) {
                previewShape.setVisible(false);
            }

            zoneSelectionRect.setVisible(false);

            if (previewInfoText != null) {
                previewInfoText.setVisible(false);
            }

            if (ghostPreview != null) {
                ghostPreview.setVisible(false);
            }

            event.consume();
        });
    }


    private void setupKeyboardHandling() {
        canvasPane.setOnKeyPressed(event -> {
            if (event.getCode() == KeyCode.DELETE) {

                deleteSelected();

                event.consume();
            }
        });
    }

    private void setupShortcuts() {
        canvasPane.addEventFilter(KeyEvent.KEY_PRESSED, e -> {
            if (e.isControlDown() && e.getCode() == KeyCode.C) {
                copySelectionToClipboard();
                e.consume();
            } else if (e.isControlDown() && e.getCode() == KeyCode.V) {
                pasteClipboard();
                e.consume();
            } else if (e.isControlDown() && e.getCode() == KeyCode.Z) {
                onUndo();
                e.consume();
            } else if (e.isControlDown() && e.getCode() == KeyCode.Y) {
                onRedo();
                e.consume();
            } else if (e.getCode() == KeyCode.PLUS || e.getCode() == KeyCode.EQUALS || "+".equals(e.getText())) {
                onLayerUp();
                e.consume();
            } else if (e.getCode() == KeyCode.MINUS || "-".equals(e.getText())) {
                onLayerDown();
                e.consume();
            } else if (!selectedRects.isEmpty()) {
                if (e.getCode() == KeyCode.LEFT) {
                    moveSelectedRects(-GRID_SIZE, 0);
                    e.consume();
                } else if (e.getCode() == KeyCode.RIGHT) {
                    moveSelectedRects(GRID_SIZE, 0);
                    e.consume();
                } else if (e.getCode() == KeyCode.UP) {
                    moveSelectedRects(0, -GRID_SIZE);
                    e.consume();
                } else if (e.getCode() == KeyCode.DOWN) {
                    moveSelectedRects(0, GRID_SIZE);
                    e.consume();
                }
            }
        });
    }

    private Point2D toGridPoint(double x, double y) {
        return new Point2D(snap(x), snap(y));
    }

    private List<Point2D> generateLinePoints(Point2D a, Point2D b) {
        List<Point2D> points = new ArrayList<>();

        int x0 = (int) (a.getX() / GRID_SIZE);
        int y0 = (int) (a.getY() / GRID_SIZE);
        int x1 = (int) (b.getX() / GRID_SIZE);
        int y1 = (int) (b.getY() / GRID_SIZE);

        int dx = Math.abs(x1 - x0);
        int dy = Math.abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true) {
            points.add(new Point2D(x0 * GRID_SIZE, y0 * GRID_SIZE));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx) {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    private void placeLine(Point2D start, Point2D end) {
        boolean changed = false;

        for (Point2D p : generateLinePoints(start, end)) {
            if (findRectangleAt(p.getX(), p.getY(), currentLayer) == null) {
                Rectangle rect = createRectangle(p.getX(), p.getY(), GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                canvasPane.getChildren().add(rect);
                sceneObjects.add(rect);
                changed = true;
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("ligne ajoutée");
    }

    private void showPreviewRect(Point2D a, Point2D b) {
        double x = Math.min(a.getX(), b.getX());
        double y = Math.min(a.getY(), b.getY());
        double w = Math.abs(a.getX() - b.getX()) + GRID_SIZE;
        double h = Math.abs(a.getY() - b.getY()) + GRID_SIZE;

        previewRect.setX(x);
        previewRect.setY(y);
        previewRect.setWidth(w);
        previewRect.setHeight(h);
        previewRect.toFront();
        previewRect.setVisible(true);
    }

    private void showZoneSelection(Point2D a, Point2D b) {
        double x = Math.min(a.getX(), b.getX());
        double y = Math.min(a.getY(), b.getY());
        double w = Math.abs(a.getX() - b.getX()) + GRID_SIZE;
        double h = Math.abs(a.getY() - b.getY()) + GRID_SIZE;

        zoneSelectionRect.setX(x);
        zoneSelectionRect.setY(y);
        zoneSelectionRect.setWidth(w);
        zoneSelectionRect.setHeight(h);
        zoneSelectionRect.toFront();
        zoneSelectionRect.setVisible(true);

        if (previewInfoText != null) {
            previewInfoText.toFront();
        }
    }


    private void placeBlock(double x, double y, int gridW, int gridH) {
        Rectangle rect = createRectangle(x, y, GRID_SIZE * gridW, GRID_SIZE * gridH, colorPicker.getValue(), currentLayer);
        rect.toFront();
        sceneObjects.add(rect);
        canvasPane.getChildren().add(rect);
        selectObject(rect);
        refreshLayerVisibility();

        updateStatus("bloc ajouté : " + selectedBlockId + " sur layer " + currentLayer);
        pushUndoSnapshot();
    }

    private void placeBrush3x3(double centerX, double centerY) {
        boolean changed = false;
        double startX = centerX - GRID_SIZE;
        double startY = centerY - GRID_SIZE;

        for (int gx = 0; gx < 3; gx++) {
            for (int gy = 0; gy < 3; gy++) {
                double x = startX + gx * GRID_SIZE;
                double y = startY + gy * GRID_SIZE;
                if (findRectangleAt(x, y, currentLayer) == null) {
                    Rectangle rect = createRectangle(x, y, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                    canvasPane.getChildren().add(rect);
                    sceneObjects.add(rect);
                    rect.toFront();
                    changed = true;
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("brush 3x3");
    }

    private void eraseAt(double x, double y) {
        Rectangle rect = findRectangleAt(x, y, currentLayer);
        if (rect != null) {
            removeDirectionText(rect);
            canvasPane.getChildren().remove(rect);
            sceneObjects.remove(rect);
            selectedRects.remove(rect);
            if (rect == selectedRect) clearSelection();
            refreshLayerVisibility();
            updateStatus("bloc supprimé");
            pushUndoSnapshot();
        }
    }

    private void placeFilledRectangle(Point2D start, Point2D end) {
        boolean changed = false;
        double minX = Math.min(start.getX(), end.getX());
        double maxX = Math.max(start.getX(), end.getX());
        double minY = Math.min(start.getY(), end.getY());
        double maxY = Math.max(start.getY(), end.getY());

        for (double x = minX; x <= maxX; x += GRID_SIZE) {
            for (double y = minY; y <= maxY; y += GRID_SIZE) {
                if (findRectangleAt(x, y, currentLayer) == null) {
                    Rectangle rect = createRectangle(x, y, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                    canvasPane.getChildren().add(rect);
                    sceneObjects.add(rect);
                    rect.toFront();
                    changed = true;
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("rectangle plein ajouté");
    }

    private void placeWallRectangle(Point2D start, Point2D end) {
        boolean changed = false;
        double minX = Math.min(start.getX(), end.getX());
        double maxX = Math.max(start.getX(), end.getX());
        double minY = Math.min(start.getY(), end.getY());
        double maxY = Math.max(start.getY(), end.getY());

        for (double x = minX; x <= maxX; x += GRID_SIZE) {
            for (double y = minY; y <= maxY; y += GRID_SIZE) {
                boolean border = x == minX || x == maxX || y == minY || y == maxY;
                if (border && findRectangleAt(x, y, currentLayer) == null) {
                    Rectangle rect = createRectangle(x, y, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                    canvasPane.getChildren().add(rect);
                    sceneObjects.add(rect);
                    rect.toFront();
                    changed = true;
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("rectangle contour ajouté");
    }

    private void placeFilledCircle(Point2D start, Point2D end) {
        boolean changed = false;
        double cx = start.getX();
        double cy = start.getY();
        double dx = end.getX() - start.getX();
        double dy = end.getY() - start.getY();
        double radius = Math.max(GRID_SIZE, Math.sqrt(dx * dx + dy * dy));

        for (double x = cx - radius; x <= cx + radius; x += GRID_SIZE) {
            for (double y = cy - radius; y <= cy + radius; y += GRID_SIZE) {
                double px = x + GRID_SIZE / 2.0;
                double py = y + GRID_SIZE / 2.0;
                double dist = Math.hypot(px - cx, py - cy);

                if (dist <= radius) {
                    double gx = snap(x);
                    double gy = snap(y);
                    if (findRectangleAt(gx, gy, currentLayer) == null) {
                        Rectangle rect = createRectangle(gx, gy, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                        canvasPane.getChildren().add(rect);
                        sceneObjects.add(rect);
                        changed = true;
                    }
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("cercle plein ajouté");
    }

    private void placeWallCircle(Point2D start, Point2D end) {
        boolean changed = false;
        double cx = start.getX();
        double cy = start.getY();
        double dx = end.getX() - start.getX();
        double dy = end.getY() - start.getY();
        double radius = Math.max(GRID_SIZE, Math.sqrt(dx * dx + dy * dy));
        double half = GRID_SIZE * 0.5;

        for (double x = cx - radius - GRID_SIZE; x <= cx + radius + GRID_SIZE; x += GRID_SIZE) {
            for (double y = cy - radius - GRID_SIZE; y <= cy + radius + GRID_SIZE; y += GRID_SIZE) {
                double px = x + GRID_SIZE / 2.0;
                double py = y + GRID_SIZE / 2.0;
                double dist = Math.hypot(px - cx, py - cy);

                if (Math.abs(dist - radius) <= half) {
                    double gx = snap(x);
                    double gy = snap(y);
                    if (findRectangleAt(gx, gy, currentLayer) == null) {
                        Rectangle rect = createRectangle(gx, gy, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                        canvasPane.getChildren().add(rect);
                        sceneObjects.add(rect);
                        changed = true;
                    }
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("cercle contour ajouté");
    }

    private void placeFilledEllipse(Point2D start, Point2D end) {
        boolean changed = false;
        double minX = Math.min(start.getX(), end.getX());
        double maxX = Math.max(start.getX(), end.getX());
        double minY = Math.min(start.getY(), end.getY());
        double maxY = Math.max(start.getY(), end.getY());

        double centerX = (minX + maxX) / 2.0;
        double centerY = (minY + maxY) / 2.0;
        double rx = Math.max(GRID_SIZE / 2.0, (maxX - minX) / 2.0);
        double ry = Math.max(GRID_SIZE / 2.0, (maxY - minY) / 2.0);

        for (double x = minX; x <= maxX; x += GRID_SIZE) {
            for (double y = minY; y <= maxY; y += GRID_SIZE) {
                double px = x + GRID_SIZE / 2.0;
                double py = y + GRID_SIZE / 2.0;

                double nx = (px - centerX) / rx;
                double ny = (py - centerY) / ry;

                if (nx * nx + ny * ny <= 1.0) {
                    double gx = snap(x);
                    double gy = snap(y);
                    if (findRectangleAt(gx, gy, currentLayer) == null) {
                        Rectangle rect = createRectangle(gx, gy, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                        canvasPane.getChildren().add(rect);
                        sceneObjects.add(rect);
                        changed = true;
                    }
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("ellipse pleine ajoutée");
    }

    private void placeWallEllipse(Point2D start, Point2D end) {
        boolean changed = false;
        double minX = Math.min(start.getX(), end.getX());
        double maxX = Math.max(start.getX(), end.getX());
        double minY = Math.min(start.getY(), end.getY());
        double maxY = Math.max(start.getY(), end.getY());

        double centerX = (minX + maxX) / 2.0;
        double centerY = (minY + maxY) / 2.0;
        double rx = Math.max(GRID_SIZE / 2.0, (maxX - minX) / 2.0);
        double ry = Math.max(GRID_SIZE / 2.0, (maxY - minY) / 2.0);

        for (double x = minX - GRID_SIZE; x <= maxX + GRID_SIZE; x += GRID_SIZE) {
            for (double y = minY - GRID_SIZE; y <= maxY + GRID_SIZE; y += GRID_SIZE) {
                double px = x + GRID_SIZE / 2.0;
                double py = y + GRID_SIZE / 2.0;

                double nx = (px - centerX) / rx;
                double ny = (py - centerY) / ry;
                double d = nx * nx + ny * ny;

                if (Math.abs(d - 1.0) <= 0.28) {
                    double gx = snap(x);
                    double gy = snap(y);
                    if (findRectangleAt(gx, gy, currentLayer) == null) {
                        Rectangle rect = createRectangle(gx, gy, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), currentLayer);
                        canvasPane.getChildren().add(rect);
                        sceneObjects.add(rect);
                        changed = true;
                    }
                }
            }
        }

        refreshLayerVisibility();
        if (changed) pushUndoSnapshot();
        updateStatus("ellipse contour ajoutée");
    }

    private Rectangle findRectangleAt(double gridX, double gridY, int layer) {
        for (Rectangle rect : sceneObjects) {
            if (getRectLayer(rect) != layer) continue;

            if (gridX >= rect.getX() && gridX < rect.getX() + rect.getWidth()
                    && gridY >= rect.getY() && gridY < rect.getY() + rect.getHeight()) {
                return rect;
            }
        }
        return null;
    }

    private Rectangle createRectangle(double x, double y, double width, double height, Color fillColor, int layer) {
        x = snap(x);
        y = snap(y);
        width = Math.max(GRID_SIZE, snap(width));
        height = Math.max(GRID_SIZE, snap(height));

        Rectangle rect = new Rectangle(x, y, width, height);
        rect.setFill(fillColor);
        rect.setStroke(Color.WHITE);
        rect.setStrokeWidth(1.5);
        rect.setMouseTransparent(false);

        Map<String, Object> data = new HashMap<>();
        data.put(USER_DATA_BLOCK_ID, selectedBlockId);
        data.put(USER_DATA_LAYER, layer);
        data.put(USER_DATA_STATE, makeDefaultStateString(selectedBlockId));
        rect.setUserData(data);

        rect.setOnMousePressed(e -> {
            if (currentTool != Tool.SELECT) return;
            if (getRectLayer(rect) != currentLayer) return;

            selectObject(rect);
            updateStatus("objet sélectionné");
            e.consume();
        });

        rect.setOnMouseDragged(e -> {
            if (currentTool != Tool.SELECT) return;
            if (getRectLayer(rect) != currentLayer) return;

            Point2D mouse = canvasPane.sceneToLocal(e.getSceneX(), e.getSceneY());

            double newX = mouse.getX() - rect.getWidth() / 2.0;
            double newY = mouse.getY() - rect.getHeight() / 2.0;

            newX = snap(newX);
            newY = snap(newY);

            newX = clamp(newX, 0, sceneWidth - rect.getWidth());
            newY = clamp(newY, 0, sceneHeight - rect.getHeight());

            rect.setX(newX);
            rect.setY(newY);

            updatePropertiesPanel(rect);
            updateResizeHandles();
            updateRectVisualDirection(rect);
            bringEditingChromeToFront();
            updateStatus("déplacement sur grille");
            e.consume();
        });

        rect.setOnMouseReleased(e -> {
            if (getRectLayer(rect) != currentLayer) return;

            rect.setX(snap(rect.getX()));
            rect.setY(snap(rect.getY()));
            rect.setX(clamp(rect.getX(), 0, sceneWidth - rect.getWidth()));
            rect.setY(clamp(rect.getY(), 0, sceneHeight - rect.getHeight()));
            updatePropertiesPanel(rect);
            updateResizeHandles();
            updateRectVisualDirection(rect);
            pushUndoSnapshot();
            refreshLayerHintMarkers();
            bringSceneNodesToFront();
            e.consume();
        });

        return rect;
    }

    private int getRectLayer(Rectangle rect) {
        Object data = rect.getUserData();
        if (data instanceof Map<?, ?> map) {
            Object layer = map.get(USER_DATA_LAYER);
            if (layer instanceof Integer i) return i;
            if (layer instanceof Number n) return n.intValue();
        }
        return 0;
    }

    private String getRectBlockId(Rectangle rect) {
        Object data = rect.getUserData();
        if (data instanceof Map<?, ?> map) {
            Object blockId = map.get(USER_DATA_BLOCK_ID);
            if (blockId != null) return blockId.toString();
        }
        return "minecraft:stone";
    }

    private String getRectBlockState(Rectangle rect) {
        Object data = rect.getUserData();
        if (data instanceof Map<?, ?> map) {
            Object state = map.get(USER_DATA_STATE);
            if (state != null) return state.toString();
        }
        return makeDefaultStateString(getRectBlockId(rect));
    }

    private void setRectBlockId(Rectangle rect, String blockId) {
        Object data = rect.getUserData();
        Map<String, Object> map;

        if (data instanceof Map<?, ?> existing) {
            map = new HashMap<>();
            for (Map.Entry<?, ?> entry : existing.entrySet()) {
                if (entry.getKey() != null) {
                    map.put(entry.getKey().toString(), entry.getValue());
                }
            }
        } else {
            map = new HashMap<>();
        }

        map.put(USER_DATA_BLOCK_ID, blockId);
        map.put(USER_DATA_LAYER, getRectLayer(rect));
        map.put(USER_DATA_STATE, makeDefaultStateString(blockId));
        rect.setUserData(map);
    }

    private void setRectBlockState(Rectangle rect, String state) {
        Object data = rect.getUserData();
        Map<String, Object> map;

        if (data instanceof Map<?, ?> existing) {
            map = new HashMap<>();
            for (Map.Entry<?, ?> entry : existing.entrySet()) {
                if (entry.getKey() != null) {
                    map.put(entry.getKey().toString(), entry.getValue());
                }
            }
        } else {
            map = new HashMap<>();
        }

        map.put(USER_DATA_BLOCK_ID, getRectBlockId(rect));
        map.put(USER_DATA_LAYER, getRectLayer(rect));
        map.put(USER_DATA_STATE, state);
        rect.setUserData(map);
    }

    private void clearLayerHintMarkers() {
        for (ImageView iv : layerHintViews) {
            canvasPane.getChildren().remove(iv);
        }
        layerHintViews.clear();
    }

    private void refreshLayerHintMarkers() {
        clearLayerHintMarkers();

        if (layerHintImage == null) return;
        if (currentLayer <= 0) return;

        for (Rectangle rect : sceneObjects) {
            int rectLayer = getRectLayer(rect);
            if (rectLayer != currentLayer - 1) continue;

            int startX = (int) (rect.getX() / GRID_SIZE);
            int startZ = (int) (rect.getY() / GRID_SIZE);
            int w = (int) (rect.getWidth() / GRID_SIZE);
            int h = (int) (rect.getHeight() / GRID_SIZE);

            for (int dx = 0; dx < w; dx++) {
                for (int dz = 0; dz < h; dz++) {
                    double cellX = (startX + dx) * GRID_SIZE;
                    double cellY = (startZ + dz) * GRID_SIZE;

                    Rectangle currentLayerBlock = findRectangleAt(cellX, cellY, currentLayer);
                    if (currentLayerBlock != null) continue;

                    ImageView iv = new ImageView(layerHintImage);
                    iv.setFitWidth(GRID_SIZE);
                    iv.setFitHeight(GRID_SIZE);
                    iv.setX(cellX);
                    iv.setY(cellY);
                    iv.setOpacity(0.65);
                    iv.setMouseTransparent(true);
                    iv.setManaged(false);

                    canvasPane.getChildren().add(iv);
                    iv.toBack();
                    layerHintViews.add(iv);
                }
            }
        }
    }

    private void refreshLayerVisibility() {
        for (Node node : canvasPane.getChildren()) {
            if (node instanceof Rectangle rect) {
                if (rect == zoneSelectionRect || rect == ghostPreview || resizeHandles.contains(rect)) {
                    continue;
                }
                if (previewShape != null) {
                    previewShape.setVisible(previewShape.isVisible());
                }
                rect.setVisible(getRectLayer(rect) == currentLayer);
            }
        }

        boolean selectedVisible = selectedRect != null && getRectLayer(selectedRect) == currentLayer;
        if (!selectedVisible) {
            hideResizeHandles();
        } else {
            updateResizeHandles();
        }

        ghostPreview.setVisible(ghostPreview.isVisible() && currentTool != Tool.SELECT && currentTool != Tool.ZONE_SELECT);
        refreshLayerHintMarkers();
        bringSceneNodesToFront();
    }

    private void selectObject(Rectangle rect) {
        clearSelectionVisualsOnly();

        selectedRect = rect;
        selectedRects.clear();

        if (rect != null) {
            selectedRects.add(rect);
            rect.setStroke(Color.CYAN);
            rect.setStrokeWidth(2.5);
            rect.toFront();
            updatePropertiesPanel(rect);
            updateResizeHandles();
            bringDirectionTextsToFront();
            bringEditingChromeToFront();
        } else {
            hideResizeHandles();
        }
    }

    private void clearSelectionVisualsOnly() {
        for (Rectangle rect : sceneObjects) {
            rect.setStroke(Color.WHITE);
            rect.setStrokeWidth(1.5);
        }
    }

    private void clearSelection() {
        clearSelectionVisualsOnly();
        selectedRect = null;
        selectedRects.clear();
        xField.clear();
        yField.clear();
        widthField.clear();
        heightField.clear();
        blockIdField.clear();
        stateField.clear();
        directionPreviewLabel.setText("-");
        hideResizeHandles();
    }

    private void updatePropertiesPanel(Rectangle rect) {
        xField.setText(format(rect.getX()));
        yField.setText(format(rect.getY()));
        widthField.setText(format(rect.getWidth()));
        heightField.setText(format(rect.getHeight()));
        blockIdField.setText(getRectBlockId(rect));
        stateField.setText(getRectBlockState(rect));

        if (rect.getFill() instanceof Color color) {
            colorPicker.setValue(color);
        }

        updateDirectionPreview(rect);
    }

    private void updateResizeHandles() {
        if (selectedRect == null || getRectLayer(selectedRect) != currentLayer || !selectedRect.isVisible()) {
            hideResizeHandles();
            return;
        }

        double x = selectedRect.getX();
        double y = selectedRect.getY();
        double w = selectedRect.getWidth();
        double h = selectedRect.getHeight();

        positionHandle(handleTopLeft, x, y);
        positionHandle(handleTopRight, x + w, y);
        positionHandle(handleBottomLeft, x, y + h);
        positionHandle(handleBottomRight, x + w, y + h);

        for (Rectangle handle : resizeHandles) {
            handle.setVisible(true);
            handle.toFront();
        }

        if (previewShape != null) {
            previewShape.toFront();
        }
        if (previewInfoText != null) {
            previewInfoText.toFront();
        }
    }

    private void positionHandle(Rectangle handle, double x, double y) {
        handle.setX(x - HANDLE_SIZE / 2.0);
        handle.setY(y - HANDLE_SIZE / 2.0);
    }

    private void hideResizeHandles() {
        for (Rectangle handle : resizeHandles) {
            handle.setVisible(false);
        }
    }

    private void updateLayerZoomLabels() {
        if (layerLabel != null) {
            layerLabel.setText("Layer : " + currentLayer);
        }
        if (zoomLabel != null) {
            double shown = canvasPane != null ? canvasPane.getScaleX() : scale;
            zoomLabel.setText("Zoom : " + (int) Math.round(shown * 100) + "%");
        }
    }

    private List<RectData> captureRectData() {
        List<RectData> out = new ArrayList<>();
        for (Rectangle r : sceneObjects) {
            out.add(new RectData(
                    r.getX(),
                    r.getY(),
                    r.getWidth(),
                    r.getHeight(),
                    getRectBlockId(r),
                    getRectBlockState(r),
                    (Color) r.getFill(),
                    getRectLayer(r)
            ));
        }
        return out;
    }

    private void restoreSnapshot(EditorSnapshot snap) {
        onNewSceneWithoutUndo();
        currentLayer = snap.currentLayer;

        for (RectData d : snap.rects) {
            Rectangle r = createRectangle(d.x, d.y, d.width, d.height, d.color, d.layer);
            setRectBlockId(r, d.blockId);
            setRectBlockState(r, d.state);
            canvasPane.getChildren().add(r);
            sceneObjects.add(r);
        }

        clearSelection();
        refreshLayerVisibility();
        updateLayerZoomLabels();
    }

    private void pushUndoSnapshot() {
        undoStack.push(new EditorSnapshot(captureRectData(), currentLayer));
        while (undoStack.size() > 100) {
            undoStack.removeLast();
        }
        redoStack.clear();
    }

    @FXML
    private void onUndo() {
        if (undoStack.size() <= 1) {
            updateStatus("rien à annuler");
            return;
        }

        EditorSnapshot current = undoStack.pop();
        redoStack.push(current);

        EditorSnapshot prev = undoStack.peek();
        if (prev != null) {
            restoreSnapshot(prev);
            updateStatus("undo");
        }
    }

    @FXML
    private void onRedo() {
        if (redoStack.isEmpty()) {
            updateStatus("rien à refaire");
            return;
        }

        EditorSnapshot next = redoStack.pop();
        restoreSnapshot(next);
        undoStack.push(new EditorSnapshot(captureRectData(), currentLayer));
        updateStatus("redo");
    }

    private void moveSelectedRects(double dx, double dy) {
        if (selectedRects.isEmpty()) return;

        dx = snap(dx);
        dy = snap(dy);

        for (Rectangle rect : selectedRects) {
            if (getRectLayer(rect) != currentLayer) continue;
            rect.setX(clamp(rect.getX() + dx, 0, sceneWidth - rect.getWidth()));
            rect.setY(clamp(rect.getY() + dy, 0, sceneHeight - rect.getHeight()));
        }

        if (selectedRect != null) {
            updatePropertiesPanel(selectedRect);
            updateResizeHandles();
        }

        pushUndoSnapshot();
        refreshLayerHintMarkers();
        bringSceneNodesToFront();
        updateStatus("déplacement sélection multiple");
    }

    private void selectZone(Point2D start, Point2D end) {
        double minX = Math.min(start.getX(), end.getX());
        double maxX = Math.max(start.getX(), end.getX());
        double minY = Math.min(start.getY(), end.getY());
        double maxY = Math.max(start.getY(), end.getY());

        clearSelection();
        selectedRects.clear();

        for (Rectangle rect : sceneObjects) {
            if (getRectLayer(rect) != currentLayer) continue;

            boolean inside =
                    rect.getX() >= minX &&
                            rect.getY() >= minY &&
                            rect.getX() + rect.getWidth() <= maxX + GRID_SIZE &&
                            rect.getY() + rect.getHeight() <= maxY + GRID_SIZE;

            if (inside) {
                selectedRects.add(rect);
                rect.setStroke(Color.YELLOW);
                rect.setStrokeWidth(2.5);
            }
        }

        if (selectedRects.size() == 1) {
            selectedRect = selectedRects.iterator().next();
            updatePropertiesPanel(selectedRect);
            updateResizeHandles();
        } else {
            selectedRect = null;
            hideResizeHandles();
        }

        updateStatus("sélection : " + selectedRects.size() + " objet(s)");
    }

    private void copySelectionToClipboard() {
        clipboard.clear();

        Collection<Rectangle> source = selectedRects.isEmpty()
                ? (selectedRect == null ? List.of() : List.of(selectedRect))
                : selectedRects;

        for (Rectangle rect : source) {
            clipboard.add(new RectData(
                    rect.getX(),
                    rect.getY(),
                    rect.getWidth(),
                    rect.getHeight(),
                    getRectBlockId(rect),
                    getRectBlockState(rect),
                    (Color) rect.getFill(),
                    getRectLayer(rect)
            ));
        }

        updateStatus("copié : " + clipboard.size() + " objet(s)");
    }

    private void pasteClipboard() {
        if (clipboard.isEmpty()) {
            updateStatus("presse-papiers vide");
            return;
        }

        clearSelection();

        for (RectData d : clipboard) {
            Rectangle r = createRectangle(d.x + GRID_SIZE, d.y + GRID_SIZE, d.width, d.height, d.color, currentLayer);
            setRectBlockId(r, d.blockId);
            setRectBlockState(r, d.state);
            canvasPane.getChildren().add(r);
            sceneObjects.add(r);
            selectedRects.add(r);
            r.setStroke(Color.YELLOW);
            r.setStrokeWidth(2.5);
        }

        if (!selectedRects.isEmpty()) {
            selectedRect = selectedRects.iterator().next();
            updatePropertiesPanel(selectedRect);
        }

        refreshLayerVisibility();
        pushUndoSnapshot();
        updateStatus("collé : " + clipboard.size() + " objet(s)");
    }

    @FXML
    private void onCopySelection() {
        copySelectionToClipboard();
    }

    private String sanitizeSceneName(String s) {
        if (s == null) return "structure";
        s = s.trim();
        if (s.isEmpty()) return "structure";
        return s.replaceAll("[\\\\/:*?\"<>|]", "_");
    }

    private String makeDefaultStateString(String blockId) {
        if (blockId == null) return "Block{minecraft:air}";

        if (blockId.contains("_stairs")) {
            return "Block{" + blockId + "}[facing=north,half=bottom,shape=straight,waterlogged=false]";
        }
        if (blockId.contains("_log") || blockId.contains("stem") || blockId.contains("hyphae") || blockId.contains("bone_block")
                || blockId.contains("chain") || blockId.contains("basalt") || blockId.contains("pillar")) {
            return "Block{" + blockId + "}[axis=y]";
        }
        if (blockId.contains("furnace") || blockId.contains("smoker") || blockId.contains("blast_furnace")
                || blockId.contains("dispenser") || blockId.contains("dropper") || blockId.contains("observer")
                || blockId.contains("piston") || blockId.contains("sticky_piston") || blockId.contains("hopper")) {
            return "Block{" + blockId + "}[facing=north]";
        }
        if (blockId.contains("_door")) {
            return "Block{" + blockId + "}[facing=north,half=lower,hinge=left,open=false,powered=false]";
        }
        if (blockId.contains("trapdoor")) {
            return "Block{" + blockId + "}[facing=north,half=bottom,open=false,powered=false,waterlogged=false]";
        }
        if (blockId.contains("fence_gate")) {
            return "Block{" + blockId + "}[facing=north,in_wall=false,open=false,powered=false]";
        }
        if (blockId.contains("redstone_wire")) {
            return "Block{" + blockId + "}[east=none,north=none,south=none,west=none,power=0]";
        }
        return "Block{" + blockId + "}";
    }

    private Map<String, String> parseBlockStateString(String stateString) {
        Map<String, String> props = new LinkedHashMap<>();
        if (stateString == null || stateString.isBlank()) return props;

        int start = stateString.indexOf('[');
        int end = stateString.indexOf(']');
        if (start < 0 || end < 0 || end <= start) return props;

        String inside = stateString.substring(start + 1, end);
        String[] pairs = inside.split(",");

        for (String pair : pairs) {
            String[] kv = pair.split("=");
            if (kv.length == 2) {
                props.put(kv[0].trim(), kv[1].trim());
            }
        }
        return props;
    }

    private String buildBlockStateString(String blockId, Map<String, String> props) {
        if (props.isEmpty()) return "Block{" + blockId + "}";
        List<String> pairs = new ArrayList<>();
        for (Map.Entry<String, String> e : props.entrySet()) {
            pairs.add(e.getKey() + "=" + e.getValue());
        }
        return "Block{" + blockId + "}[" + String.join(",", pairs) + "]";
    }

    private String rotateFacing90(String facing) {
        return switch (facing) {
            case "north" -> "east";
            case "east" -> "south";
            case "south" -> "west";
            case "west" -> "north";
            default -> facing;
        };
    }

    private String rotateFacingMinus90(String facing) {
        return switch (facing) {
            case "north" -> "west";
            case "west" -> "south";
            case "south" -> "east";
            case "east" -> "north";
            default -> facing;
        };
    }

    private static String normProp(Map<String, String> props, String key) {
        if (props == null || !props.containsKey(key)) {
            return "";
        }
        String v = props.get(key);
        return v == null ? "" : v.trim();
    }

    /** Glyphes sur le canevas seulement si facing / axis / rotation diffèrent du défaut pour ce bloc. */
    private boolean hasOrientationOverlay(String blockId, String stateString) {
        if (blockId == null) {
            blockId = "";
        }
        Map<String, String> cur = parseBlockStateString(stateString);
        Map<String, String> def = parseBlockStateString(makeDefaultStateString(blockId));

        return !normProp(cur, "rotation").equals(normProp(def, "rotation"))
                || !normProp(cur, "facing").equals(normProp(def, "facing"))
                || !normProp(cur, "axis").equals(normProp(def, "axis"));
    }

    private String getDirectionGlyph(String blockId, String stateString) {
        if (blockId == null) {
            blockId = "";
        }
        Map<String, String> props = parseBlockStateString(stateString);
        String rotationPart = props.containsKey("rotation") ? ("↻" + props.get("rotation")) : "";

        String core;
        if (blockId.contains("_stairs")) {
            String facing = props.getOrDefault("facing", "north");
            core = switch (facing) {
                case "north" -> "↑";
                case "south" -> "↓";
                case "east" -> "→";
                case "west" -> "←";
                default -> "S";
            };
        } else if (blockId.contains("_log") || blockId.contains("stem") || blockId.contains("hyphae")
                || blockId.contains("bone_block") || blockId.contains("chain")
                || blockId.contains("basalt") || blockId.contains("pillar")) {
            String axis = props.getOrDefault("axis", "y");
            core = switch (axis) {
                case "x" -> "X";
                case "y" -> "Y";
                case "z" -> "Z";
                default -> "L";
            };
        } else if (props.containsKey("facing")) {
            String facing = props.get("facing");
            core = switch (facing) {
                case "north" -> "↑";
                case "south" -> "↓";
                case "east" -> "→";
                case "west" -> "←";
                case "up" -> "U";
                case "down" -> "D";
                default -> "?";
            };
        } else if (blockId.contains("redstone_wire")) {
            core = "R";
        } else {
            core = "-";
        }

        if ("-".equals(core) && !rotationPart.isEmpty()) {
            return rotationPart;
        }
        if (!rotationPart.isEmpty()) {
            return core + rotationPart;
        }
        return core;
    }

    private void updateDirectionPreview(Rectangle rect) {
        if (rect == null) {
            directionPreviewLabel.setText("-");
            return;
        }

        String glyph = getDirectionGlyph(getRectBlockId(rect), getRectBlockState(rect));
        directionPreviewLabel.setText(glyph + "   " + getRectBlockState(rect));
    }

    private String rotateBlockStateY90(String blockId, String stateString) {
        Map<String, String> props = parseBlockStateString(stateString);

        if (props.containsKey("facing")) {
            props.put("facing", rotateFacing90(props.get("facing")));
        }

        if (blockId.contains("_log") || blockId.contains("stem") || blockId.contains("hyphae")
                || blockId.contains("bone_block") || blockId.contains("chain")
                || blockId.contains("basalt") || blockId.contains("pillar")) {
            String axis = props.getOrDefault("axis", "y");
            if ("x".equals(axis)) props.put("axis", "z");
            else if ("z".equals(axis)) props.put("axis", "x");
        }

        if (blockId.contains("redstone_wire")) {
            String north = props.getOrDefault("north", "none");
            String east = props.getOrDefault("east", "none");
            String south = props.getOrDefault("south", "none");
            String west = props.getOrDefault("west", "none");

            props.put("east", north);
            props.put("south", east);
            props.put("west", south);
            props.put("north", west);
        }

        if (blockId.contains("_stairs")) {
            String shape = props.getOrDefault("shape", "straight");
            if ("inner_left".equals(shape)) props.put("shape", "inner_right");
            else if ("inner_right".equals(shape)) props.put("shape", "inner_left");
            else if ("outer_left".equals(shape)) props.put("shape", "outer_right");
            else if ("outer_right".equals(shape)) props.put("shape", "outer_left");
        }

        return buildBlockStateString(blockId, props);
    }

    private String rotateBlockStateYMinus90(String blockId, String stateString) {
        Map<String, String> props = parseBlockStateString(stateString);

        if (props.containsKey("facing")) {
            props.put("facing", rotateFacingMinus90(props.get("facing")));
        }

        if (blockId.contains("_log") || blockId.contains("stem") || blockId.contains("hyphae")
                || blockId.contains("bone_block") || blockId.contains("chain")
                || blockId.contains("basalt") || blockId.contains("pillar")) {
            String axis = props.getOrDefault("axis", "y");
            if ("x".equals(axis)) props.put("axis", "z");
            else if ("z".equals(axis)) props.put("axis", "x");
        }

        if (blockId.contains("redstone_wire")) {
            String north = props.getOrDefault("north", "none");
            String east = props.getOrDefault("east", "none");
            String south = props.getOrDefault("south", "none");
            String west = props.getOrDefault("west", "none");

            props.put("west", north);
            props.put("north", east);
            props.put("east", south);
            props.put("south", west);
        }

        if (blockId.contains("_stairs")) {
            String shape = props.getOrDefault("shape", "straight");
            if ("inner_left".equals(shape)) props.put("shape", "inner_right");
            else if ("inner_right".equals(shape)) props.put("shape", "inner_left");
            else if ("outer_left".equals(shape)) props.put("shape", "outer_right");
            else if ("outer_right".equals(shape)) props.put("shape", "outer_left");
        }

        return buildBlockStateString(blockId, props);
    }

    @FXML
    private void onRotateSelected() {
        if (selectedRect == null) {
            updateStatus("aucun objet sélectionné");
            return;
        }

        String blockId = getRectBlockId(selectedRect);
        String oldState = getRectBlockState(selectedRect);
        String newState = rotateBlockStateY90(blockId, oldState);

        setRectBlockState(selectedRect, newState);
        updatePropertiesPanel(selectedRect);
        updateDirectionPreview(selectedRect);
        pushUndoSnapshot();
        updateStatus("rotation état +90°");
        bringSceneNodesToFront();

    }

    @FXML
    private void onRotateSelectedMinus90() {
        if (selectedRect == null) {
            updateStatus("aucun objet sélectionné");
            return;
        }

        String blockId = getRectBlockId(selectedRect);
        String oldState = getRectBlockState(selectedRect);
        String newState = rotateBlockStateYMinus90(blockId, oldState);

        setRectBlockState(selectedRect, newState);
        updatePropertiesPanel(selectedRect);
        updateDirectionPreview(selectedRect);
        pushUndoSnapshot();
        updateStatus("rotation état -90°");
        bringSceneNodesToFront();

    }

    @FXML
    private void onSetAxisX() {
        setAxisOnSelected("x");
    }

    @FXML
    private void onSetAxisY() {
        setAxisOnSelected("y");
    }

    @FXML
    private void onSetAxisZ() {
        setAxisOnSelected("z");
    }

    private void setAxisOnSelected(String axis) {
        if (selectedRect == null) {
            updateStatus("aucun objet sélectionné");
            return;
        }

        String blockId = getRectBlockId(selectedRect);
        String state = getRectBlockState(selectedRect);
        Map<String, String> props = parseBlockStateString(state);

        props.put("axis", axis);
        setRectBlockState(selectedRect, buildBlockStateString(blockId, props));

        updatePropertiesPanel(selectedRect);
        updateDirectionPreview(selectedRect);
        pushUndoSnapshot();
        updateStatus("axe " + axis);
        bringSceneNodesToFront();
    }

    @FXML private void onSelectTool() { currentTool = Tool.SELECT; updateStatus("outil sélection"); }
    @FXML private void onLineTool() { currentTool = Tool.LINE; updateStatus("outil ligne"); }
    @FXML private void onBlock1x1Tool() { currentTool = Tool.BLOCK_1X1; updateStatus("outil bloc 1x1"); }
    @FXML private void onBlock2x1Tool() { currentTool = Tool.BLOCK_2X1; updateStatus("outil bloc 2x1"); }
    @FXML private void onBlock2x2Tool() { currentTool = Tool.BLOCK_2X2; updateStatus("outil bloc 2x2"); }
    @FXML private void onBrush3x3Tool() { currentTool = Tool.BRUSH_3X3; updateStatus("outil brush 3x3"); }
    @FXML private void onEraserTool() { currentTool = Tool.ERASER_1X1; updateStatus("outil gomme"); }
    @FXML private void onFillRectTool() { currentTool = Tool.FILL_RECT; updateStatus("outil rectangle plein"); }
    @FXML private void onWallRectTool() { currentTool = Tool.WALL_RECT; updateStatus("outil rectangle contour"); }
    @FXML private void onFillCircleTool() { currentTool = Tool.FILL_CIRCLE; updateStatus("outil cercle plein"); }
    @FXML private void onWallCircleTool() { currentTool = Tool.WALL_CIRCLE; updateStatus("outil cercle contour"); }
    @FXML private void onFillEllipseTool() { currentTool = Tool.FILL_ELLIPSE; updateStatus("outil ellipse pleine"); }
    @FXML private void onWallEllipseTool() { currentTool = Tool.WALL_ELLIPSE; updateStatus("outil ellipse contour"); }
    @FXML private void onZoneSelectTool() { currentTool = Tool.ZONE_SELECT; updateStatus("outil sélection de zone"); }

    @FXML
    private void onLayerUp() {
        currentLayer = Math.min(255, currentLayer + 1);
        clearSelection();
        refreshLayerVisibility();
        updateLayerZoomLabels();
        updateStatus("layer " + currentLayer);
    }

    @FXML
    private void onLayerDown() {
        currentLayer = Math.max(0, currentLayer - 1);
        clearSelection();
        refreshLayerVisibility();
        updateLayerZoomLabels();
        updateStatus("layer " + currentLayer);
    }

    @FXML
    private void onApplyProperties() {
        if (selectedRect == null) {
            updateStatus("aucun objet sélectionné");
            updateRectVisualDirection(selectedRect);
            return;

        }

        try {
            double x = Double.parseDouble(xField.getText().replace(",", "."));
            double y = Double.parseDouble(yField.getText().replace(",", "."));
            double w = Double.parseDouble(widthField.getText().replace(",", "."));
            double h = Double.parseDouble(heightField.getText().replace(",", "."));

            String customState = stateField.getText() == null ? "" : stateField.getText().trim();
            String blockId = blockIdField.getText() == null || blockIdField.getText().isBlank()
                    ? selectedBlockId
                    : blockIdField.getText().trim();

            x = snap(x);
            y = snap(y);
            w = Math.max(GRID_SIZE, snap(w));
            h = Math.max(GRID_SIZE, snap(h));

            x = clamp(x, 0, sceneWidth - w);
            y = clamp(y, 0, sceneHeight - h);

            selectedRect.setX(x);
            selectedRect.setY(y);
            selectedRect.setWidth(w);
            selectedRect.setHeight(h);
            selectedRect.setFill(colorPicker.getValue());

            setRectBlockId(selectedRect, blockId);
            selectedBlockId = blockId;

            if (!customState.isBlank()) {
                setRectBlockState(selectedRect, customState);
            }

            selectedRect.toFront();

            updatePropertiesPanel(selectedRect);
            updateResizeHandles();
            bringSceneNodesToFront();
            pushUndoSnapshot();
            updateStatus("propriétés appliquées");
        } catch (Exception ex) {
            updateStatus("valeurs invalides");
        }
    }

    @FXML
    private void onDeleteSelected() {

        deleteSelected();
    }

    private void deleteSelected() {
        Collection<Rectangle> targets = !selectedRects.isEmpty()
                ? new ArrayList<>(selectedRects)
                : (selectedRect == null ? List.of() : List.of(selectedRect));

        if (targets.isEmpty()) {
            updateStatus("aucun objet à supprimer");
            return;
        }

        for (Rectangle r : targets) {
            removeDirectionText(r);
            canvasPane.getChildren().remove(r);
            sceneObjects.remove(r);
        }

        clearSelection();
        refreshLayerVisibility();
        pushUndoSnapshot();

        updateStatus("objet(s) supprimé(s)");
    }

    private void onNewSceneWithoutUndo() {
        List<Rectangle> copy = new ArrayList<>(sceneObjects);
        for (Rectangle rect : copy) {
            removeDirectionText(rect);
            canvasPane.getChildren().remove(rect);
        }
        clearLayerHintMarkers();
        sceneObjects.clear();
        selectedRects.clear();
        selectedRect = null;
        if (previewShape != null) {
            previewShape.setVisible(false);
        }
        if (previewInfoText != null) {
            previewInfoText.setVisible(false);
        }
        zoneSelectionRect.setVisible(false);
        ghostPreview.setVisible(false);
        clearSelection();
    }

    @FXML
    private void onNewScene() {
        onNewSceneWithoutUndo();
        currentLayer = 0;
        currentSceneName = "structure";
        sceneNameField.setText(currentSceneName);
        updateLayerZoomLabels();
        refreshLayerVisibility();
        updateStatus("nouvelle creation");
        pushUndoSnapshot();
    }

    private String buildLegacyJson(String structureName) {
        List<String> blocks = new ArrayList<>();

        for (Rectangle r : sceneObjects) {
            String blockId = getRectBlockId(r);
            String state = getRectBlockState(r);

            int startX = (int) (r.getX() / GRID_SIZE);
            int startZ = (int) (r.getY() / GRID_SIZE);
            int w = (int) (r.getWidth() / GRID_SIZE);
            int h = (int) (r.getHeight() / GRID_SIZE);
            int layer = getRectLayer(r);

            for (int dx = 0; dx < w; dx++) {
                for (int dz = 0; dz < h; dz++) {
                    int bx = startX + dx;
                    int by = layer;
                    int bz = startZ + dz;

                    blocks.add(
                            "{\"x\":" + bx +
                                    ",\"y\":" + by +
                                    ",\"z\":" + bz +
                                    ",\"blockId\":\"" + escapeJson(blockId) +
                                    "\",\"blockStateString\":\"" + escapeJson(state) +
                                    "\"}"
                    );
                }
            }
        }

        int sizeXBlocks = (int) Math.ceil(sceneWidth / GRID_SIZE);
        int sizeZBlocks = (int) Math.ceil(sceneHeight / GRID_SIZE);
        int maxLayer = sceneObjects.stream().mapToInt(this::getRectLayer).max().orElse(0);
        int sizeYBlocks = Math.max(256, maxLayer + 1);

        return "{"
                + "\"name\":\"" + escapeJson(structureName) + "\","
                + "\"sizeX\":" + sizeXBlocks + ","
                + "\"sizeY\":" + sizeYBlocks + ","
                + "\"sizeZ\":" + sizeZBlocks + ","
                + "\"blocks\":["
                + String.join(",", blocks)
                + "]"
                + "}";
    }

    private String buildCompactJson(String structureName) {
        Map<String, Integer> paletteIndex = new LinkedHashMap<>();
        List<String> palette = new ArrayList<>();
        List<Integer> positions = new ArrayList<>();
        List<Integer> states = new ArrayList<>();

        for (Rectangle r : sceneObjects) {
            String blockId = getRectBlockId(r);
            String state = getRectBlockState(r);
            String key = blockId + "|" + state;

            int index = paletteIndex.computeIfAbsent(key, k -> {
                palette.add(k);
                return palette.size() - 1;
            });

            int startX = (int) (r.getX() / GRID_SIZE);
            int startZ = (int) (r.getY() / GRID_SIZE);
            int w = (int) (r.getWidth() / GRID_SIZE);
            int h = (int) (r.getHeight() / GRID_SIZE);
            int layer = getRectLayer(r);

            for (int dx = 0; dx < w; dx++) {
                for (int dz = 0; dz < h; dz++) {
                    positions.add(startX + dx);
                    positions.add(layer);
                    positions.add(startZ + dz);
                    states.add(index);
                }
            }
        }

        String paletteJson = palette.stream()
                .map(s -> "\"" + escapeJson(s) + "\"")
                .collect(Collectors.joining(","));

        String positionsJson = positions.stream()
                .map(String::valueOf)
                .collect(Collectors.joining(","));

        String statesJson = states.stream()
                .map(String::valueOf)
                .collect(Collectors.joining(","));

        int sizeXBlocks = (int) Math.ceil(sceneWidth / GRID_SIZE);
        int sizeZBlocks = (int) Math.ceil(sceneHeight / GRID_SIZE);
        int maxLayer = sceneObjects.stream().mapToInt(this::getRectLayer).max().orElse(0);
        int sizeYBlocks = Math.max(256, maxLayer + 1);

        return "{"
                + "\"name\":\"" + escapeJson(structureName) + "\","
                + "\"sizeX\":" + sizeXBlocks + ","
                + "\"sizeY\":" + sizeYBlocks + ","
                + "\"sizeZ\":" + sizeZBlocks + ","
                + "\"format\":2,"
                + "\"palette\":[" + paletteJson + "],"
                + "\"positions\":[" + positionsJson + "],"
                + "\"states\":[" + statesJson + "]"
                + "}";
    }

    private String buildWrapperJson(String structureName) {
        String compact = buildCompactJson(structureName);
        return "{\"structures\":{\"" + escapeJson(structureName.toLowerCase(Locale.ROOT)) + "\":" + compact + "}}";
    }

    @FXML
    private void onApplyCanvasSize() {
        if (canvasWidthField == null || canvasHeightField == null) {
            return;
        }
        try {
            double w = Double.parseDouble(canvasWidthField.getText().trim().replace(",", "."));
            double h = Double.parseDouble(canvasHeightField.getText().trim().replace(",", "."));
            if (!Double.isFinite(w) || !Double.isFinite(h) || w < GRID_SIZE || h < GRID_SIZE) {
                updateStatus("largeur/hauteur invalides (minimum " + (int) GRID_SIZE + ")");
                return;
            }
            sceneWidth = w;
            sceneHeight = h;
            canvasPane.setPrefSize(sceneWidth, sceneHeight);
            canvasPane.setMinSize(sceneWidth, sceneHeight);
            canvasPane.setMaxSize(Double.MAX_VALUE, Double.MAX_VALUE);
            scheduleGridRedraw();
            bringSceneNodesToFront();
            updateStatus("taille création : " + String.format(Locale.US, "%.0f", sceneWidth)
                    + " × " + String.format(Locale.US, "%.0f", sceneHeight));
        } catch (NumberFormatException ex) {
            updateStatus("largeur/hauteur invalides");
        }
    }

    @FXML
    private void onSaveLegacy() {
        String structureName = sanitizeSceneName(sceneNameField.getText());
        currentSceneName = structureName;
        sceneNameField.setText(structureName);
        saveJsonToFile(buildLegacyJson(structureName), structureName + ".json");
    }

    @FXML
    private void onSaveCompact() {
        String structureName = sanitizeSceneName(sceneNameField.getText());
        currentSceneName = structureName;
        sceneNameField.setText(structureName);
        saveJsonToFile(buildCompactJson(structureName), structureName + "_compact.json");
    }

    @FXML
    private void onSaveWrapper() {
        String structureName = sanitizeSceneName(sceneNameField.getText());
        currentSceneName = structureName;
        sceneNameField.setText(structureName);
        saveJsonToFile(buildWrapperJson(structureName), structureName + "_saveddata.json");
    }

    private void saveJsonToFile(String json, String defaultName) {
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Sauvegarder JSON");
        chooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("JSON Files", "*.json"));
        chooser.setInitialFileName(defaultName);

        Window ownerWindow = canvasPane != null && canvasPane.getScene() != null ? canvasPane.getScene().getWindow() : null;
        File file = chooser.showSaveDialog(ownerWindow);
        if (file == null) return;

        try {
            Path targetPath = ensureJsonExtension(file.toPath());
            writeJsonAtomically(targetPath, json);
            updateStatus("sauvegarde : " + targetPath.getFileName());
        } catch (IOException e) {
            updateStatus("erreur sauvegarde");
        }
    }

    private Path ensureJsonExtension(Path selectedPath) {
        String fileName = selectedPath.getFileName().toString();
        if (fileName.toLowerCase(Locale.ROOT).endsWith(".json")) {
            return selectedPath;
        }
        Path parent = selectedPath.getParent();
        String jsonFileName = fileName + ".json";
        return parent == null ? Path.of(jsonFileName) : parent.resolve(jsonFileName);
    }

    private void writeJsonAtomically(Path targetPath, String json) throws IOException {
        Path absoluteTarget = targetPath.toAbsolutePath();
        Path parent = absoluteTarget.getParent();
        if (parent != null) {
            Files.createDirectories(parent);
        }

        Path tempFile = parent != null
                ? Files.createTempFile(parent, absoluteTarget.getFileName().toString(), ".tmp")
                : Files.createTempFile(absoluteTarget.getFileName().toString(), ".tmp");

        try {
            Files.writeString(tempFile, json, StandardCharsets.UTF_8);
            try {
                Files.move(tempFile, absoluteTarget,
                        StandardCopyOption.REPLACE_EXISTING,
                        StandardCopyOption.ATOMIC_MOVE);
            } catch (IOException atomicMoveNotSupported) {
                Files.move(tempFile, absoluteTarget, StandardCopyOption.REPLACE_EXISTING);
            }
        } finally {
            Files.deleteIfExists(tempFile);
        }
    }

    @FXML
    private void onLoadLegacy() {
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Ouvrir JSON Legacy");
        chooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("JSON Files", "*.json"));

        File file = chooser.showOpenDialog(canvasPane.getScene().getWindow());
        if (file == null) return;

        try {
            String json = Files.readString(file.toPath(), StandardCharsets.UTF_8);
            loadLegacyFromJson(json);
            pushUndoSnapshot();
            updateStatus("legacy chargé");
        } catch (Exception e) {
            updateStatus("erreur chargement");
        }
    }

    private void loadLegacyFromJson(String json) {
        onNewSceneWithoutUndo();

        Pattern namePattern = Pattern.compile("\"name\":\"([^\"]+)\"");
        Matcher nameMatcher = namePattern.matcher(json);
        if (nameMatcher.find()) {
            currentSceneName = nameMatcher.group(1);
            sceneNameField.setText(currentSceneName);
        }

        Pattern p = Pattern.compile(
                "\\{\"x\":(\\d+),\"y\":(\\d+),\"z\":(\\d+),\"blockId\":\"([^\"]+)\",\"blockStateString\":\"([^\"]*)\"\\}"
        );

        Matcher m = p.matcher(json);

        while (m.find()) {
            int bx = Integer.parseInt(m.group(1));
            int by = Integer.parseInt(m.group(2));
            int bz = Integer.parseInt(m.group(3));
            String blockId = m.group(4);
            String state = m.group(5);

            Rectangle rect = createRectangle(bx * GRID_SIZE, bz * GRID_SIZE, GRID_SIZE, GRID_SIZE, colorPicker.getValue(), by);
            setRectBlockId(rect, blockId);
            setRectBlockState(rect, state == null || state.isBlank() ? makeDefaultStateString(blockId) : state);
            canvasPane.getChildren().add(rect);
            sceneObjects.add(rect);
        }

        clearSelection();
        refreshLayerVisibility();
        updateLayerZoomLabels();
    }

    private String escapeJson(String s) {
        return s.replace("\\", "\\\\").replace("\"", "\\\"");
    }

    private double snap(double value) {
        if (snapCheckBox != null && !snapCheckBox.isSelected()) {
            return value;
        }
        return Math.round(value / GRID_SIZE) * GRID_SIZE;
    }

    private String format(double value) {
        return String.format(Locale.US, "%.0f", value);
    }

    private void updateStatus(String text) {
        if (statusLabel != null) {
            statusLabel.setText("Statut : " + text);
        } else {
            System.out.println("Statut : " + text);
        }
    }

    private double clamp(double value, double min, double max) {
        return Math.max(min, Math.min(max, value));
    }
    private void updateRectVisualDirection(Rectangle rect) {
        if (rect == null) return;
        if (!rect.isVisible()) {
            removeDirectionText(rect);
            return;
        }

        Object old = rect.getProperties().get(USER_DATA_DIRECTION_TEXT);
        if (old instanceof Text oldText) {
            canvasPane.getChildren().remove(oldText);
        }

        String blockId = getRectBlockId(rect);
        String state = getRectBlockState(rect);
        if (!hasOrientationOverlay(blockId, state)) {
            rect.getProperties().remove(USER_DATA_DIRECTION_TEXT);
            return;
        }

        String glyph = getDirectionGlyph(blockId, state);
        if (glyph == null || glyph.equals("-")) {
            rect.getProperties().remove(USER_DATA_DIRECTION_TEXT);
            return;
        }

        Text text = new Text(glyph);
        text.setMouseTransparent(true);
        text.setManaged(false);
        styleDirectionOverlayText(text, rect, glyph);

        double centerX = rect.getX() + rect.getWidth() / 2.0;
        double centerY = rect.getY() + rect.getHeight() / 2.0;

        text.applyCss();
        double textWidth = text.getLayoutBounds().getWidth();
        double textHeight = text.getLayoutBounds().getHeight();

        text.setX(centerX - textWidth / 2.0);
        text.setY(centerY + textHeight / 4.0);

        canvasPane.getChildren().add(text);
        text.toFront();

        rect.getProperties().put(USER_DATA_DIRECTION_TEXT, text);
    }

    private void styleDirectionOverlayText(Text text, Rectangle rect, String glyph) {
        double cell = Math.min(rect.getWidth(), rect.getHeight());
        double fontSize = Math.max(18, Math.min(44, cell * 0.55));

        Color fill = Color.web("#FFEB3B");
        if (glyph.contains("↑") || glyph.contains("↓") || glyph.contains("←") || glyph.contains("→")) {
            fill = Color.web("#76FF03");
        }
        if (glyph.contains("↻")) {
            fill = Color.web("#FFCA28");
        }
        if ("X".equals(glyph)) {
            fill = Color.web("#69F0AE");
        } else if ("Y".equals(glyph)) {
            fill = Color.web("#18FFFF");
        } else if ("Z".equals(glyph)) {
            fill = Color.web("#EA80FC");
        }

        text.setFill(fill);
        text.setStroke(Color.web("#101010"));
        text.setStrokeWidth(1.35);
        text.setFont(Font.font("Segoe UI", FontWeight.BLACK, fontSize));
    }

    private void removeDirectionText(Rectangle rect) {
        Object old = rect.getProperties().get(USER_DATA_DIRECTION_TEXT);
        if (old instanceof Text oldText) {
            canvasPane.getChildren().remove(oldText);
        }
        rect.getProperties().remove(USER_DATA_DIRECTION_TEXT);
    }


}