package legacyforgepatcher;

import java.util.concurrent.Executor;

import net.minecraft.client.Minecraft;
import net.minecraft.client.multiplayer.ClientPacketListener;
import net.minecraft.network.protocol.Packet;

/**
 * Sends a client's packet from the client thread. Minecraft from 1.20.5 answers a chunk batch from the network thread at once, which can land between the
 * client thread sending the end of configuration and switching outgoing packets to play, when the pipeline has no outgoing protocol and the client drops its
 * connection. The client thread does both inside one task, so a send it runs afterwards finds play set up. It is defined in ClientPacketListener's loader.
 */
public final class ClientPackets {
    private ClientPackets() {
    }

    /** Calls listener.send(packet) on the client thread: at once there, and queued behind the running task from any other thread. */
    public static void send(final ClientPacketListener listener, final Packet<?> packet) {
        // Minecraft is a BlockableEventLoop, which is an Executor that runs tasks on the client thread.
        Executor client = Minecraft.getInstance();

        client.execute(new Runnable() {
            @Override
            public void run() {
                listener.send(packet);
            }
        });
    }
}
