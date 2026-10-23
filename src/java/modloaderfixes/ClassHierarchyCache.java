package modloaderfixes;

import java.util.Collections;
import java.util.HashMap;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Makes ModLauncher's cache of class hierarchies safe to share between threads, as ModLauncher 8 does. ModLauncher 2 to 5 keep it in three static HashMaps that
 * every thread transforming a class reads and writes without a lock, and a thread records a class's parent before its hierarchy: another thread that finds the
 * parent reads the hierarchy as null, and the game crashes with a NullPointerException in TransformerClassWriter.getCommonSuperClass. TransformerClassWriter's
 * class initializer gets its maps from here instead. Each one locks the get, put and containsKey that ModLauncher calls, a class counts as known only once its
 * hierarchy and its interface flag are recorded too, so a thread that finds it half recorded records it again itself, and each hierarchy is a concurrent set,
 * which the thread recording it may still be adding interfaces to. It names no game type and lives in the bootstrap loader.
 */
public final class ClassHierarchyCache {
    // The class initializer creates the parents first and then the other two, so the parents look the others up when asked.
    private static volatile Hierarchies hierarchies;
    private static volatile Interfaces interfaces;

    private ClassHierarchyCache() {
    }

    /** Stands in for classParents' new HashMap(). */
    public static HashMap<String, String> parents() {
        return new Parents();
    }

    /** Stands in for classHierarchies' new HashMap(). */
    public static HashMap<String, Set<String>> hierarchies() {
        Hierarchies created = new Hierarchies();

        hierarchies = created;
        return created;
    }

    /** Stands in for isInterface's new HashMap(). */
    public static HashMap<String, Boolean> interfaces() {
        Interfaces created = new Interfaces();

        interfaces = created;
        return created;
    }

    private static class Locked<V> extends HashMap<String, V> {
        private static final long serialVersionUID = 1L;

        @Override
        public synchronized boolean containsKey(Object key) {
            return super.containsKey(key);
        }

        @Override
        public synchronized V get(Object key) {
            return super.get(key);
        }

        @Override
        public synchronized V put(String key, V value) {
            return super.put(key, value);
        }
    }

    private static final class Parents extends Locked<String> {
        private static final long serialVersionUID = 1L;

        // ModLauncher asks whether it knows a class before it reads the class's hierarchy and interface flag, which it records after its parent.
        @Override
        public boolean containsKey(Object key) {
            Hierarchies knownHierarchies = hierarchies;
            Interfaces knownInterfaces = interfaces;

            return super.containsKey(key) && knownHierarchies != null && knownHierarchies.containsKey(key) && knownInterfaces != null && knownInterfaces.containsKey(key);
        }
    }

    private static final class Hierarchies extends Locked<Set<String>> {
        private static final long serialVersionUID = 1L;

        // ModLauncher adds a class's interfaces to its hierarchy after it records it, and reads and copies hierarchies that another thread may be adding to.
        @Override
        public Set<String> put(String key, Set<String> value) {
            Set<String> concurrent = Collections.newSetFromMap(new ConcurrentHashMap<String, Boolean>());

            concurrent.addAll(value);
            return super.put(key, concurrent);
        }
    }

    private static final class Interfaces extends Locked<Boolean> {
        private static final long serialVersionUID = 1L;
    }
}
