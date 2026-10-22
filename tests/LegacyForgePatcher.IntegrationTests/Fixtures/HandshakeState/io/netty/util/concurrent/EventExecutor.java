package io.netty.util.concurrent;

import java.util.concurrent.Executor;

/** The part of Netty's EventExecutor that the agent's helper calls. */
public interface EventExecutor extends Executor {
    boolean inEventLoop();
}
