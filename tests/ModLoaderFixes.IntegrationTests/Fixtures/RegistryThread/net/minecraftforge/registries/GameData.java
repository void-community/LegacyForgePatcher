package net.minecraftforge.registries;

import com.google.common.collect.Multimap;

import java.util.Map;

/** Rebuilds the registries, and says which thread it did it on. */
public class GameData {
    public static void revertToFrozen() {
        System.out.println("REVERT on " + Thread.currentThread().getName());
    }

    public static Multimap<String, String> injectSnapshot(final Map<String, String> snapshot, boolean injectFrozenData, boolean isLocalWorld) {
        System.out.println("INJECT " + snapshot + " on " + Thread.currentThread().getName());

        return new Multimap<String, String>() {
            @Override
            public int size() {
                return snapshot.size();
            }
        };
    }
}
