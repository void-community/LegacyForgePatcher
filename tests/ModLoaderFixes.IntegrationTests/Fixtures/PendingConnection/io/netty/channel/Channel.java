package io.netty.channel;

/** The part of Netty's channel that Minecraft 1.7's NetworkManager asks before it ticks a connection. */
public interface Channel {
    boolean isOpen();
}
