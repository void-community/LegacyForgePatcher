package net.minecraftforge.fml.loading;

import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import com.electronwill.nightconfig.core.file.CommentedFileConfig;
import com.electronwill.nightconfig.core.file.GenericBuilder;

/** Loads fml.toml as Forge from Minecraft 1.13.2 to 1.19.4 does: with autosave and autoreload, then corrects each outdated value while it walks the config. */
public class FMLConfig {
    private CommentedFileConfig configData;

    private void loadFrom(Path configFile) {
        configData = new GenericBuilder().autosave().autoreload().build();
        configData.load();

        // ConfigSpec.correct walks the config and sets each value that doesn't fit the spec.
        List<String> corrected = new ArrayList<>();

        for (Map.Entry<String, Object> entry : configData.valueMap().entrySet()) {
            if ("outdated".equals(entry.getValue())) {
                configData.set(entry.getKey(), "default");
                corrected.add(entry.getKey());
            }
        }

        configData.save();
        System.out.println("CORRECTED " + corrected.size() + " values, RELOADED " + configData.reloads() + " times");
    }

    public static void main(String[] arguments) {
        try {
            new FMLConfig().loadFrom(Paths.get("config", "fml.toml"));
        } catch (RuntimeException exception) {
            System.out.println("CRASHED " + exception);
            System.exit(1);
        }
    }
}
