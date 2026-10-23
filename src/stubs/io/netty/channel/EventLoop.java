package io.netty.channel;

import java.util.concurrent.Executor;

/** The part of Netty's event loop that the helpers use; Netty declares inEventLoop on EventExecutor, which EventLoop extends, and the JVM finds it there. */
public interface EventLoop extends Executor {
    boolean inEventLoop();
}
