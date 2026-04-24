module com.magicalchalkstudion.magicalchalkstudio {
    requires javafx.controls;
    requires javafx.fxml;


    opens com.magicalchalkstudion.magicalchalkstudio to javafx.fxml;
    exports com.magicalchalkstudion.magicalchalkstudio;
}