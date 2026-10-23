package net.minecraft.client.multiplayer;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicReference;

import net.minecraft.client.Minecraft;
import net.minecraft.network.Connection;
import net.minecraft.network.protocol.game.ClientboundChunkBatchFinishedPacket;

/**
 * A proxy moves a playing client to another server as Minecraft from 26.1 sees it: the network thread answers a chunk batch, and the client thread then handles
 * the start of configuration, which gives the connection a configuration listener and outgoing protocol, before it runs the tasks queued meanwhile.
 */
public final class ServerSwitch {
    private ServerSwitch() {
    }

    public static void main(String[] arguments) throws InterruptedException {
        Minecraft client = Minecraft.getInstance();
        Connection connection = new Connection();
        final ClientPacketListener listener = new ClientPacketListener(connection);
        final AtomicReference<Throwable> dropped = new AtomicReference<>();
        final CountDownLatch handled = new CountDownLatch(1);

        connection.setupInboundProtocol(listener);
        connection.setupOutboundProtocol("play");

        Thread network = new Thread(() -> {
            try {
                listener.handleChunkBatchFinished(new ClientboundChunkBatchFinishedPacket());
            } catch (RuntimeException exception) {
                dropped.set(exception);
            } finally {
                handled.countDown();
            }
        }, "Netty Epoll Client IO #1");

        network.start();
        handled.await();

        // handleConfigurationStart, from the packet queue, which the client thread drains before its tasks.
        connection.setupInboundProtocol(new ClientConfigurationPacketListenerImpl(connection));
        connection.setupOutboundProtocol("configuration");

        try {
            client.runAllTasks();
        } catch (RuntimeException exception) {
            dropped.set(exception);
        }

        if (dropped.get() != null) {
            System.out.println("DROPPED " + dropped.get().getMessage());
            System.exit(1);
        }

        System.out.println("SWITCHED to configuration");
    }
}
