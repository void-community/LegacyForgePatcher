package net.minecraft.network;

import java.util.ArrayList;
import java.util.List;
import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import io.netty.channel.Channel;

/** A connection as Minecraft 1.7 keeps it, by FML's names: it has no channel until channelActive, and processing its packets reads the channel at once. */
public class NetworkManager {
    private final String name;
    private final Queue<String> received = new ConcurrentLinkedQueue<String>();
    private final List<String> answered = new ArrayList<String>();
    private Channel field_150746_k;
    private boolean disconnected;

    public NetworkManager(String name) {
        this.name = name;
    }

    /** The network thread's channelActive, which gives the manager its channel. */
    public void channelActive(Channel channel) {
        field_150746_k = channel;
    }

    /** A packet the network thread queued for the server thread, such as the login start that follows a handshake. */
    public void receive(String packet) {
        received.add(packet);
    }

    /** isChannelOpen. */
    public boolean func_150724_d() {
        return field_150746_k != null && field_150746_k.isOpen();
    }

    /** processReceivedPackets, which starts by reading the channel's state, as Minecraft 1.7's does. */
    public void func_74428_b() {
        if (!field_150746_k.isOpen())
            throw new IllegalStateException(name + " processed after its channel closed");

        for (String packet = received.poll(); packet != null; packet = received.poll())
            answered.add(packet);
    }

    /** What the tick tells the handler of a connection it takes for closed; a handshake handler does nothing with it. */
    void disconnected() {
        disconnected = true;
    }

    public String describe() {
        return name + (disconnected ? " dropped" : " kept") + ", answered " + answered;
    }
}
