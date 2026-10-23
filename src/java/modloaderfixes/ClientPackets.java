package modloaderfixes;

import java.util.concurrent.Executor;

import net.minecraft.client.Minecraft;
import net.minecraft.client.multiplayer.ClientPacketListener;
import net.minecraft.network.Connection;
import net.minecraft.network.protocol.Packet;

/**
 * Sends a client's packet from the client thread. Minecraft from 1.20.5 answers a chunk batch from the network thread at once, which can land between the
 * client thread sending the end of configuration and switching outgoing packets to play, when the pipeline has no outgoing protocol and the client drops its
 * connection. The client thread does both inside one task, so a send it runs afterwards finds play set up. Minecraft from 26.1 handles packets before it runs
 * tasks, so a server switch can take the connection back into configuration, which has no chunk batch answer, before the send runs; the answer then belongs
 * to a play phase that has ended, and is dropped. It is defined in ClientPacketListener's loader.
 */
public final class ClientPackets {
    private ClientPackets() {
    }

    /**
     * Calls listener.send(packet) on the client thread: at once there, and queued behind the running task from any other thread, unless the connection has closed
     * or passed to another listener by then.
     */
    public static void send(final ClientPacketListener listener, final Packet<?> packet) {
        // Minecraft is a BlockableEventLoop, which is an Executor that runs tasks on the client thread.
        Executor client = Minecraft.getInstance();

        client.execute(new Runnable() {
            @Override
            public void run() {
                Connection connection = listener.getConnection();

                // The server, or the next one behind a proxy, starts a chunk batch of its own when play resumes, so nothing waits for a dropped answer.
                if (connection.isConnected() && connection.getPacketListener() == listener)
                    listener.send(packet);
            }
        });
    }
}
