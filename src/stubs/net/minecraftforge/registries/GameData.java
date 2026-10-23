package net.minecraftforge.registries;

import java.util.Map;

import com.google.common.collect.Multimap;

/** Forge 1.12's registry data. */
public class GameData {
    public static void revertToFrozen() {
        throw new UnsupportedOperationException();
    }

    public static Multimap<?, ?> injectSnapshot(Map<?, ?> snapshot, boolean injectFrozenData, boolean isLocalWorld) {
        throw new UnsupportedOperationException();
    }
}
