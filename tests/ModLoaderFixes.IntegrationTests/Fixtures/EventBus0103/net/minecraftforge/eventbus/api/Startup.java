package net.minecraftforge.eventbus.api;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import net.minecraftforge.fml.common.gameevent.TickEvent;

/** Starts a mod-loading worker and the client thread as Forge 1.14 does, and stops them in the order that deadlocks under EventBus's lock. */
public final class Startup {
    public static final CountDownLatch WORKER_BUILDING = new CountDownLatch(1);
    public static final CountDownLatch CLIENT_INITIALIZING = new CountDownLatch(1);

    private Startup() {
    }

    public static void await(CountDownLatch latch) {
        try {
            latch.await(2, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }

    public static void main(String[] arguments) throws InterruptedException {
        Thread worker = new Thread(() -> EventListenerHelper.getListenerList(TickEvent.PlayerTickEvent.class), "modloading-worker-1");
        Thread client = new Thread(TickEvent::listeners, "Client thread");
        long deadline = System.currentTimeMillis() + 10000;

        worker.start();
        client.start();
        worker.join(Math.max(1, deadline - System.currentTimeMillis()));
        client.join(Math.max(1, deadline - System.currentTimeMillis()));

        if (worker.isAlive() || client.isAlive()) {
            System.out.println("DEADLOCK between the mod-loading worker and the client thread");
            System.exit(1);
        }

        System.out.println("STARTED with " + (EventListenerHelper.getListenerList(TickEvent.class) == TickEvent.listeners() ? "one" : "two") + " TickEvent listener list");
    }
}
