package modloaderfixes;

import io.netty.channel.EventLoop;
import net.minecraft.network.EnumConnectionState;
import net.minecraft.network.NetworkManager;

/**
 * Switches a Minecraft connection to another state on the connection's event loop, as vanilla's NetworkManager.dispatchPacket does, so the packets
 * already queued there are encoded in the state they were sent in. It is defined in the loader of FML's NetworkDispatcher, the class it is called from.
 */
public final class ConnectionStates {
    private ConnectionStates() {
    }

    /** Calls manager.setConnectionState(state) on the event loop of the manager's channel: at once on that loop, and queued behind its tasks elsewhere. */
    public static void setConnectionState(final NetworkManager manager, final EnumConnectionState state) {
        EventLoop eventLoop = manager.channel().eventLoop();

        if (eventLoop.inEventLoop()) {
            manager.func_150723_a(state);
            return;
        }

        eventLoop.execute(new Runnable() {
            @Override
            public void run() {
                manager.func_150723_a(state);
            }
        });
    }
}
