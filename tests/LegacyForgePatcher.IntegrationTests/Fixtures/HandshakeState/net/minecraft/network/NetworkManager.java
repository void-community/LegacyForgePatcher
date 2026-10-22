package net.minecraft.network;

import io.netty.channel.Channel;
import io.netty.util.concurrent.EventExecutor;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.TimeUnit;

/** Sends packets as Minecraft 1.12 does from the server thread: queued on the channel's event loop, which encodes each in the state the connection has by then. */
public class NetworkManager {
    private final List<String> sent = new ArrayList<String>();
    private final List<String> failed = new ArrayList<String>();
    private final CountDownLatch held = new CountDownLatch(1);
    private Thread loopThread;
    private final ExecutorService loop = Executors.newSingleThreadExecutor(new ThreadFactory() {
        @Override
        public Thread newThread(Runnable task) {
            loopThread = new Thread(task, "Netty Server IO #1");
            return loopThread;
        }
    });
    private final EventExecutor eventLoop = new EventExecutor() {
        @Override
        public boolean inEventLoop() {
            return Thread.currentThread() == loopThread;
        }

        @Override
        public void execute(Runnable task) {
            loop.execute(task);
        }
    };
    private final Channel channel = new Channel() {
        @Override
        public EventExecutor eventLoop() {
            return eventLoop;
        }
    };
    private volatile EnumConnectionState state = EnumConnectionState.LOGIN;

    public Channel channel() {
        return channel;
    }

    /** setConnectionState, by the name FML's deobfuscation gives it. */
    public void func_150723_a(EnumConnectionState newState) {
        state = newState;
    }

    public void sendPacket(final String packet, final EnumConnectionState packetState) {
        eventLoop.execute(new Runnable() {
            @Override
            public void run() {
                // The play state has no id for a login packet, so Minecraft can't encode it and closes the connection.
                if (state != packetState)
                    failed.add(packet + " in " + state);
                else
                    sent.add(state + " " + packet);
            }
        });
    }

    /** Keeps the event loop busy, as a loaded server's is, so the packets sent next wait in its queue. */
    public void hold() {
        eventLoop.execute(new Runnable() {
            @Override
            public void run() {
                try {
                    held.await();
                } catch (InterruptedException exception) {
                    Thread.currentThread().interrupt();
                }
            }
        });
    }

    /** Lets the event loop run what was queued, and waits until it has. */
    public void drain() throws InterruptedException {
        held.countDown();
        loop.shutdown();

        if (!loop.awaitTermination(10, TimeUnit.SECONDS))
            throw new IllegalStateException("The event loop did not finish");
    }

    public List<String> sent() {
        return sent;
    }

    public List<String> failed() {
        return failed;
    }
}
