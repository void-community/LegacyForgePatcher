package net.minecraft.client;

import com.google.common.util.concurrent.ListenableFuture;

import java.util.concurrent.Callable;
import java.util.concurrent.Executors;
import java.util.concurrent.FutureTask;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;

/** Runs scheduled tasks as Minecraft's client does: at once on the client thread, and queued for its next tick from any other thread. */
public class Minecraft {
    private static final Minecraft INSTANCE = new Minecraft();

    private final LinkedBlockingQueue<Runnable> scheduled = new LinkedBlockingQueue<Runnable>();
    private final Thread clientThread = Thread.currentThread();

    /** getMinecraft, by the name FML's deobfuscation gives it. */
    public static Minecraft func_71410_x() {
        return INSTANCE;
    }

    /** addScheduledTask(Callable). */
    public <V> ListenableFuture<V> func_152343_a(Callable<V> task) {
        Task<V> future = new Task<V>(task);

        if (Thread.currentThread() == clientThread)
            future.run();
        else
            scheduled.add(future);

        return future;
    }

    /** addScheduledTask(Runnable). */
    public ListenableFuture<Object> func_152344_a(Runnable task) {
        return func_152343_a(Executors.callable(task));
    }

    /** Ticks the client until the given thread has finished and nothing is left to run. */
    public void tickUntil(Thread network) throws InterruptedException {
        while (network.isAlive() || !scheduled.isEmpty()) {
            Runnable task = scheduled.poll(10, TimeUnit.MILLISECONDS);

            if (task != null)
                task.run();
        }
    }

    private static final class Task<V> extends FutureTask<V> implements ListenableFuture<V> {
        Task(Callable<V> task) {
            super(task);
        }
    }
}
