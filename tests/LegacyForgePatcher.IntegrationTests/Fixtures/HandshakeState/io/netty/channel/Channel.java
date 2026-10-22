package io.netty.channel;

import io.netty.util.concurrent.EventExecutor;

/** The part of Netty's Channel that the agent's helper calls. */
public interface Channel {
    EventExecutor eventLoop();
}
