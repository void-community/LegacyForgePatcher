package legacyforgepatcher;

import java.nio.file.Path;

/**
 * Loads FML's config once. FML loads fml.toml twice while the game starts, and its early loading window starts reading the config between the two loads.
 * FMLConfig.loadFrom stores a new, empty config before it reads the file into it, so the window can read a setting in between and find nothing. The second load
 * reads the same file a moment after the first, so this skips it. It is defined in FMLConfig's loader, and reaches configData and loadFrom, which are private,
 * through the getter and invoker the patch adds.
 */
public final class ModLoaderConfigs {
    private ModLoaderConfigs() {
    }

    /** Calls Forge's config.loadFrom(file), unless config already holds the loaded file. */
    public static void loadFrom(net.minecraftforge.fml.loading.FMLConfig config, Path file) {
        if (config.legacyforgepatcher$configData() == null)
            config.legacyforgepatcher$loadFrom(file);
    }

    /** Calls NeoForge's config.loadFrom(file), unless config already holds the loaded file. */
    public static void loadFrom(net.neoforged.fml.loading.FMLConfig config, Path file) {
        if (config.legacyforgepatcher$configData() == null)
            config.legacyforgepatcher$loadFrom(file);
    }
}
