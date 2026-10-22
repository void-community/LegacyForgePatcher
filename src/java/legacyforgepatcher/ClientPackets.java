package legacyforgepatcher;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.concurrent.Executor;

/**
 * Sends a client's packet from the client thread. Minecraft from 1.20.5 answers a chunk batch from the network thread at once, which can land between the
 * client thread sending the end of configuration and switching outgoing packets to play, when the pipeline has no outgoing protocol and the client drops its
 * connection. The client thread does both inside one task, so a send it runs afterwards finds play set up. It lives in the bootstrap loader and reaches
 * Minecraft through the listener's loader and reflection.
 */
public final class ClientPackets {
    private ClientPackets() {
    }

    /** Calls listener.send(packet) on the client thread: at once there, and queued behind the running task from any other thread. */
    public static void send(final Object listener, final Object packet) throws ReflectiveOperationException {
        ClassLoader loader = listener.getClass().getClassLoader();
        final Method send = listener.getClass().getMethod("send", Class.forName("net.minecraft.network.protocol.Packet", false, loader));
        // Minecraft is a BlockableEventLoop, which is an Executor that runs tasks on the client thread.
        Executor client = (Executor) Class.forName("net.minecraft.client.Minecraft", false, loader).getMethod("getInstance").invoke(null);

        client.execute(new Runnable() {
            @Override
            public void run() {
                try {
                    send.invoke(listener, packet);
                } catch (InvocationTargetException exception) {
                    Throwable cause = exception.getCause();

                    if (cause instanceof RuntimeException)
                        throw (RuntimeException) cause;

                    if (cause instanceof Error)
                        throw (Error) cause;

                    throw new IllegalStateException(cause);
                } catch (IllegalAccessException exception) {
                    throw new IllegalStateException(exception);
                }
            }
        });
    }
}
