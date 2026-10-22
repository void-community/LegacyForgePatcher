package net.minecraft.util.thread;

import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.Executor;

/** Runs tasks on its own thread: at once there, and queued from any other, as Minecraft's does. */
public class BlockableEventLoop implements Executor {
    private final ConcurrentLinkedQueue<Runnable> pending = new ConcurrentLinkedQueue<>();
    private final Thread thread = Thread.currentThread();

    @Override
    public void execute(Runnable task) {
        if (Thread.currentThread() == thread)
            task.run();
        else
            pending.add(task);
    }

    public void runAllTasks() {
        for (Runnable task = pending.poll(); task != null; task = pending.poll())
            task.run();
    }
}
