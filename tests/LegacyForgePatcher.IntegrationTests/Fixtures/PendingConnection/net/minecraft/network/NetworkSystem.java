package net.minecraft.network;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Iterator;
import java.util.List;

import io.netty.channel.Channel;

/**
 * Minecraft 1.7's network tick, by FML's names: it drops each manager whose channel isn't open and processes the rest. Its main method runs the tick between
 * the channel initializer adding a manager and channelActive giving it its channel, beside a manager whose channel has closed, then lets the new manager's
 * login start arrive and ticks again.
 */
public class NetworkSystem {
    private final List<NetworkManager> field_151272_f = Collections.synchronizedList(new ArrayList<NetworkManager>());

    /** networkTick. */
    public void func_151269_c() {
        synchronized (field_151272_f) {
            Iterator<NetworkManager> iterator = field_151272_f.iterator();

            while (iterator.hasNext()) {
                NetworkManager manager = iterator.next();

                if (!manager.func_150724_d()) {
                    iterator.remove();
                    manager.disconnected();
                } else {
                    try {
                        manager.func_74428_b();
                    } catch (Exception exception) {
                        System.out.println("FAILED to handle packet: " + exception);
                    }
                }
            }
        }
    }

    public static void main(String[] arguments) {
        NetworkSystem system = new NetworkSystem();
        NetworkManager joining = new NetworkManager("joining");
        NetworkManager leaving = new NetworkManager("leaving");

        leaving.channelActive(channel(false));
        // The channel initializer adds the new manager on the network thread, and the server thread ticks before channelActive.
        system.field_151272_f.add(joining);
        system.field_151272_f.add(leaving);
        system.func_151269_c();

        joining.channelActive(channel(true));
        joining.receive("login start");
        system.func_151269_c();

        System.out.println(joining.describe() + "; " + leaving.describe());
        System.exit(joining.describe().equals("joining kept, answered [login start]") && leaving.describe().startsWith("leaving dropped") ? 0 : 1);
    }

    private static Channel channel(final boolean open) {
        return new Channel() {
            @Override
            public boolean isOpen() {
                return open;
            }
        };
    }
}
