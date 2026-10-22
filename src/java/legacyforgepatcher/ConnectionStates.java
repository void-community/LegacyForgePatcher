package legacyforgepatcher;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.concurrent.Executor;

/**
 * Switches a Minecraft connection to another state on the connection's event loop, as vanilla's NetworkManager.dispatchPacket does, so the packets
 * already queued there are encoded in the state they were sent in. It lives in the bootstrap loader, which can't see Minecraft or Netty, so it
 * reaches them through reflection.
 */
public final class ConnectionStates {
    private ConnectionStates() {
    }

    /** Calls manager.setConnectionState(state) on the event loop of the manager's channel: at once on that loop, and queued behind its tasks elsewhere. */
    public static void setConnectionState(final Object manager, final Object state) throws ReflectiveOperationException {
        // The states are enum constants with bodies, so the parameter type is the declaring enum, not the constant's class.
        final Method setConnectionState = manager.getClass().getMethod("func_150723_a", ((Enum<?>) state).getDeclaringClass());
        Object channel = manager.getClass().getMethod("channel").invoke(manager);
        ClassLoader loader = channel.getClass().getClassLoader();
        // The channel's class may not be public, so call through Netty's public interfaces.
        Object eventLoop = Class.forName("io.netty.channel.Channel", false, loader).getMethod("eventLoop").invoke(channel);
        boolean inEventLoop = (Boolean) Class.forName("io.netty.util.concurrent.EventExecutor", false, loader).getMethod("inEventLoop").invoke(eventLoop);

        if (inEventLoop) {
            invoke(setConnectionState, manager, state);
            return;
        }

        ((Executor) eventLoop).execute(new Runnable() {
            @Override
            public void run() {
                try {
                    invoke(setConnectionState, manager, state);
                } catch (IllegalAccessException exception) {
                    throw new IllegalStateException(exception);
                }
            }
        });
    }

    private static void invoke(Method setConnectionState, Object manager, Object state) throws IllegalAccessException {
        try {
            setConnectionState.invoke(manager, state);
        } catch (InvocationTargetException exception) {
            Throwable cause = exception.getCause();

            if (cause instanceof RuntimeException)
                throw (RuntimeException) cause;

            if (cause instanceof Error)
                throw (Error) cause;

            throw new IllegalStateException(cause);
        }
    }
}
