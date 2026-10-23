package net.minecraftforge.registries;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Consumer;
import java.util.function.Predicate;

/**
 * Keeps object holder handlers as Forge does from Minecraft 1.13.2 to 1.21.4: in a static set that addHandler adds to without a lock, which mods call from the
 * mod-loading worker threads at once. The class creates a map of named handlers too, as Forge for 1.20.1 does, which isn't shared.
 */
public class ObjectHolderRegistry {
    private static final Set<Consumer<Predicate<String>>> objectHolders = new HashSet<>();
    private static final Map<String, Object> namedHandlers = new HashMap<>();
    private static final int THREADS = 8;
    private static final int HANDLERS_PER_THREAD = 50000;

    public static void addHandler(Consumer<Predicate<String>> handler) {
        objectHolders.add(handler);
    }

    public static void main(String[] arguments) throws InterruptedException {
        final AtomicReference<Throwable> crashed = new AtomicReference<>();
        List<Thread> workers = new ArrayList<>();

        for (int worker = 0; worker < THREADS; worker++) {
            Thread thread = new Thread(() -> {
                // Each handler captures its own name, so each is a distinct object, as the handlers of different fields are.
                for (int handler = 0; handler < HANDLERS_PER_THREAD; handler++) {
                    String name = Thread.currentThread().getName() + "/" + handler;

                    addHandler(filter -> filter.test(name));
                }
            }, "modloading-worker-" + worker);

            thread.setUncaughtExceptionHandler((failed, exception) -> crashed.set(exception));
            workers.add(thread);
        }

        for (Thread thread : workers)
            thread.start();

        // A hash map that two threads change at once can also loop forever, so a worker that hasn't finished in time counts as broken too.
        long deadline = System.currentTimeMillis() + 10000;

        for (Thread thread : workers) {
            thread.join(Math.max(1, deadline - System.currentTimeMillis()));

            if (thread.isAlive()) {
                System.out.println("HUNG in addHandler");
                System.exit(1);
            }
        }

        int expected = THREADS * HANDLERS_PER_THREAD;

        if (crashed.get() != null) {
            System.out.println("CRASHED " + crashed.get());
            System.exit(1);
        }

        if (objectHolders.size() != expected) {
            System.out.println("LOST " + (expected - objectHolders.size()) + " of " + expected + " handlers");
            System.exit(1);
        }

        System.out.println("HANDLERS " + objectHolders.size() + " in " + objectHolders.getClass().getSimpleName() + ", NAMED " + namedHandlers.getClass().getSimpleName());
    }
}
