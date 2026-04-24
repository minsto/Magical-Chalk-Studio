package com.magicalchalkstudion.magicalchalkstudio;

import javafx.application.Application;
import javafx.fxml.FXMLLoader;
import javafx.scene.Scene;
import javafx.scene.image.Image;
import javafx.stage.Stage;

import java.io.IOException;
import java.net.URL;
import java.util.Objects;

public class HelloApplication extends Application {
    @Override
    public void start(Stage stage) throws IOException {
        URL fxmlUrl = Objects.requireNonNull(
                HelloApplication.class.getResource("hello-view.fxml"),
                "Missing resource: hello-view.fxml"
        );
        FXMLLoader fxmlLoader = new FXMLLoader(fxmlUrl);
        Scene scene = new Scene(fxmlLoader.load(), 1500, 1000);
        stage.setTitle("Magical Chalk Studio");
        stage.setScene(scene);

        URL iconUrl = HelloApplication.class.getResource("app-icon.png");
        if (iconUrl != null) {
            stage.getIcons().add(new Image(iconUrl.toExternalForm(), true));
        }

        URL cssUrl = HelloApplication.class.getResource("style.css");
        if (cssUrl != null) {
            scene.getStylesheets().add(cssUrl.toExternalForm());
        } else {
            System.err.println("Warning: style.css not found, starting without stylesheet.");
        }

        stage.show();
    }

    public static void main(String[] args) {
        launch();
    }
}