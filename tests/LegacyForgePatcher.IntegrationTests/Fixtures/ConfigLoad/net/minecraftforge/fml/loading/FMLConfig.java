package net.minecraftforge.fml.loading;

import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Loads FML's config as Forge and NeoForge do from Minecraft 1.20.1: loadFrom stores a new, empty config before it reads the file into it, FML loads the
 * config twice while the game starts, and the early loading window reads it from its own thread in between.
 */
public class FMLConfig {
    private static final FMLConfig INSTANCE = new FMLConfig();
    private static final AtomicInteger LOADS = new AtomicInteger();
    private static final CountDownLatch SECOND_LOAD = new CountDownLatch(1);
    private static final CountDownLatch WINDOW_READ = new CountDownLatch(1);

    private Map<String, Object> configData;

    // The second load waits, between storing the empty config and filling it, until the window has read a setting, as a slow disk or a busy CPU can make it.
    private void loadFrom(Path configFile) {
        configData = new HashMap<>();

        if (LOADS.incrementAndGet() == 2) {
            SECOND_LOAD.countDown();
            await(WINDOW_READ);
        }

        configData.put("earlyWindowSquir", Boolean.FALSE);
    }

    public static void load() {
        INSTANCE.loadFrom(Path.of("config", "fml.toml"));
    }

    public static boolean getBoolConfigValue(String key) {
        return (Boolean) INSTANCE.configData.get(key);
    }

    public static void main(String[] arguments) throws InterruptedException {
        AtomicReference<Throwable> crashed = new AtomicReference<>();

        // ModDirTransformerDiscoverer.candidates loads the config, then earlyInitialization starts the window.
        load();

        Thread window = new Thread(() -> {
            await(SECOND_LOAD);

            try {
                System.out.println("SQUIR " + getBoolConfigValue("earlyWindowSquir"));
            } finally {
                WINDOW_READ.countDown();
            }
        }, "EarlyDisplay");

        window.setUncaughtExceptionHandler((thread, exception) -> crashed.set(exception));
        window.start();
        // FMLServiceProvider.initialize loads it again.
        load();
        window.join();

        if (crashed.get() != null) {
            System.out.println("WINDOW CRASHED " + crashed.get());
            System.exit(1);
        }

        System.out.println("LOADS " + LOADS.get());
    }

    private static void await(CountDownLatch latch) {
        try {
            latch.await(1, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
