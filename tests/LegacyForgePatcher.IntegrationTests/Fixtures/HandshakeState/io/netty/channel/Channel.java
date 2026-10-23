package io.netty.channel;

/** The part of Netty's Channel that the agent's helper calls. */
public interface Channel {
    EventLoop eventLoop();
}
