package io.netty.channel;

import io.netty.util.concurrent.EventExecutor;

/** Netty's EventLoop, which a Channel's eventLoop() returns and which takes inEventLoop and execute from EventExecutor. */
public interface EventLoop extends EventExecutor {
}
