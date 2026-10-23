package net.minecraft.network;

import io.netty.channel.Channel;

/** Minecraft's connection, as Forge 1.7 and 1.12 run it with FML's names. */
public class NetworkManager {
    /** Forge's getter of the connection's channel, which is null until channelActive. */
    public Channel channel() {
        throw new UnsupportedOperationException();
    }

    /** isChannelOpen. */
    public boolean func_150724_d() {
        throw new UnsupportedOperationException();
    }

    /** processReceivedPackets. */
    public void func_74428_b() {
        throw new UnsupportedOperationException();
    }

    /** setConnectionState. */
    public void func_150723_a(EnumConnectionState state) {
        throw new UnsupportedOperationException();
    }
}
