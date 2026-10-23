package net.minecraft.client.multiplayer;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicReference;

import net.minecraft.client.Minecraft;
import net.minecraft.network.Connection;
import net.minecraft.network.PacketListener;
import net.minecraft.network.protocol.game.ClientboundChunkBatchFinishedPacket;
import net.minecraft.network.protocol.game.ServerboundChunkBatchReceivedPacket;

/** Answers a chunk batch at once on the network thread, as Minecraft does from 1.20.5, while the client thread finishes configuration. */
public class ClientPacketListener extends ClientCommonPacketListenerImpl implements PacketListener {
    public ClientPacketListener(Connection connection) {
        super(connection);
    }

    public void handleChunkBatchFinished(ClientboundChunkBatchFinishedPacket packet) {
        this.send(new ServerboundChunkBatchReceivedPacket(5.25F));
    }

    public static void main(String[] arguments) throws InterruptedException {
        // The game starts the client on its main thread, which becomes the client thread.
        Minecraft client = Minecraft.getInstance();
        Connection connection = new Connection();
        ClientPacketListener listener = new ClientPacketListener(connection);
        AtomicReference<Throwable> dropped = new AtomicReference<>();
        CountDownLatch handled = new CountDownLatch(1);

        // handleConfigurationFinished: the play listener takes the connection's packets, and the finish packet is terminal, so the pipeline has no outgoing
        // protocol until play is set up.
        connection.setupInboundProtocol(listener);
        connection.setupOutboundProtocol(null);

        // The server's first chunk batch arrives in that gap, while the client thread is still busy.
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
        connection.setupOutboundProtocol("play");
        client.runAllTasks();

        if (dropped.get() != null) {
            System.out.println("DROPPED " + dropped.get().getMessage());
            System.exit(1);
        }
    }
}
