package legacyforgepatcher;

import net.minecraft.network.NetworkManager;

/**
 * Lets Minecraft 1.7's network tick leave a connection alone until it has a channel, as Minecraft 1.8 does. NetworkSystem's channel initializer adds each
 * new NetworkManager to the list the server thread ticks before channelActive gives it its channel, and the tick takes a manager without one for a closed
 * connection: it drops the manager from the list without a word, and the login that the network thread goes on to start is never answered or timed out.
 * It is defined in NetworkSystem's loader and calls the manager by the names FML gives it.
 */
public final class Connections {
    private Connections() {
    }

    /** Stands in for manager.isChannelOpen() in the tick: a manager that has no channel yet counts as open, so the tick keeps it. */
    public static boolean isOpenOrPending(NetworkManager manager) {
        return manager.channel() == null || manager.func_150724_d();
    }

    /** Stands in for manager.processReceivedPackets() in the tick, which reads the channel at once: a manager that has no channel yet waits for a later tick. */
    public static void processIfActive(NetworkManager manager) {
        if (manager.channel() != null)
            manager.func_74428_b();
    }
}
