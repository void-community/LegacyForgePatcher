package legacyforgepatcher;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.nio.file.Path;

/**
 * Loads FML's config once. FML loads fml.toml twice while the game starts, and its early loading window starts reading the config between the two loads.
 * FMLConfig.loadFrom stores a new, empty config before it reads the file into it, so the window can read a setting in between and find nothing. The second load
 * reads the same file a moment after the first, so this skips it. It lives in the bootstrap loader and reaches FMLConfig, whose packages FML's automatic
 * module opens to every module, through reflection: NeoForge's FML 4 changed configData from a CommentedFileConfig to a CommentedConfig, and a helper compiled
 * against either type would fail to link on the other.
 */
public final class ModLoaderConfigs {
    private ModLoaderConfigs() {
    }

    /** Calls config.loadFrom(file), unless config already holds the loaded file. */
    public static void loadFrom(Object config, Path file) throws ReflectiveOperationException {
        Class<?> type = config.getClass();
        Field configData = type.getDeclaredField("configData");

        configData.setAccessible(true);

        if (configData.get(config) != null)
            return;

        Method loadFrom = type.getDeclaredMethod("loadFrom", Path.class);

        loadFrom.setAccessible(true);

        try {
            loadFrom.invoke(config, file);
        } catch (InvocationTargetException exception) {
            Throwable cause = exception.getCause();

            if (cause instanceof RuntimeException)
                throw (RuntimeException) cause;

            if (cause instanceof Error)
                throw (Error) cause;

            throw new IllegalStateException(cause);
        }
    }
}
