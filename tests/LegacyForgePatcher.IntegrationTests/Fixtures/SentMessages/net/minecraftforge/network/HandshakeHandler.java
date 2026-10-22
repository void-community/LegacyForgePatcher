package net.minecraftforge.network;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Keeps the login messages that wait for a reply as Forge's handshake handler does, from Minecraft 1.13 to 1.20.1: the server thread adds each message it sends,
 * and the network thread removes each one the client answers with removeIf. The constructor creates another list too, as from 1.18, which isn't shared.
 */
public class HandshakeHandler {
    private List<Integer> sentMessages = new ArrayList<>();
    private final List<String> packetBacklog = new ArrayList<>();

    void tickServer(int packetPosition) {
        sentMessages.add(packetPosition);
    }

    // The predicate waits a moment on its first element, as a busy network thread can, so the server thread's add lands while removeIf runs.
    void handleIndexedMessage(final int index, final CountDownLatch removing, final CountDownLatch added) {
        final boolean[] first = { true };

        sentMessages.removeIf(i -> {
            if (first[0]) {
                first[0] = false;
                removing.countDown();
                await(added);
            }

            return i == index;
        });
    }

    public static void main(String[] arguments) throws InterruptedException {
        final HandshakeHandler handler = new HandshakeHandler();
        final CountDownLatch removing = new CountDownLatch(1);
        final CountDownLatch added = new CountDownLatch(1);
        final AtomicReference<Throwable> dropped = new AtomicReference<>();

        handler.tickServer(1);
        handler.tickServer(2);

        Thread network = new Thread(() -> handler.handleIndexedMessage(1, removing, added), "Netty Server IO #1");

        network.setUncaughtExceptionHandler((thread, exception) -> dropped.set(exception));
        network.start();
        removing.await();
        // On a CopyOnWriteArrayList this add waits for removeIf to finish.
        handler.tickServer(3);
        added.countDown();
        network.join();

        if (dropped.get() != null) {
            System.out.println("DROPPED " + dropped.get());
            System.exit(1);
        }

        System.out.println("SENT " + handler.sentMessages.getClass().getSimpleName() + " " + handler.sentMessages + ", BACKLOG " + handler.packetBacklog.getClass().getSimpleName());
    }

    private static void await(CountDownLatch latch) {
        try {
            latch.await(1, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
