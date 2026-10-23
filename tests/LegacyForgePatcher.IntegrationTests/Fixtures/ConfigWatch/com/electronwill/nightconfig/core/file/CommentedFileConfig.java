package com.electronwill.nightconfig.core.file;

import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.concurrent.SynchronousQueue;
import java.util.concurrent.TimeUnit;

/**
 * A config backed by a file, as NightConfig's is: with autosave each change saves the file, and with autoreload a watcher thread reloads the file into the config
 * after each save. A save here waits for that reload, as a watcher that is quick enough lands while the config is still in use.
 */
public class CommentedFileConfig {
    private final boolean autosave;
    private final boolean autoreload;
    private final SynchronousQueue<String> saved = new SynchronousQueue<>();
    private final SynchronousQueue<String> reloaded = new SynchronousQueue<>();
    private final Map<String, Object> values = new HashMap<>();
    private final Map<String, Object> file = new LinkedHashMap<>();
    private int reloads;

    CommentedFileConfig(boolean autosave, boolean autoreload) {
        this.autosave = autosave;
        this.autoreload = autoreload;
        // The default fml.toml that FML copies on a first start, which still has an outdated value for each key.
        file.put("earlyWindowControl", "outdated");
        file.put("maxThreads", "outdated");
        file.put("versionCheck", "outdated");

        if (autoreload) {
            Thread watcher = new Thread(this::watch, "FileWatcher");

            watcher.setDaemon(true);
            watcher.start();
        }
    }

    public void load() {
        values.putAll(file);
    }

    public Map<String, Object> valueMap() {
        return values;
    }

    public void set(String key, Object value) {
        values.put(key, value);

        if (autosave)
            save();
    }

    public void save() {
        file.clear();
        file.putAll(values);

        if (!autoreload)
            return;

        try {
            if (saved.offer("saved", 1, TimeUnit.SECONDS))
                reloaded.poll(1, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }

    public int reloads() {
        return reloads;
    }

    // Reloads the file into the config after each save, replacing every entry.
    private void watch() {
        try {
            while (true) {
                saved.take();
                values.clear();
                values.putAll(file);
                reloads++;
                reloaded.put("reloaded");
            }
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
