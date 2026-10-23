package legacyforgepatcher;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;

/**
 * Lets Minecraft 1.7's network tick leave a connection alone until it has a channel, as Minecraft 1.8 does. NetworkSystem's channel initializer adds each
 * new NetworkManager to the list the server thread ticks before channelActive gives it its channel, and the tick takes a manager without one for a closed
 * connection: it drops the manager from the list without a word, and the login that the network thread goes on to start is never answered or timed out.
 * It lives in the bootstrap loader, which can't see Minecraft, so it reaches the manager through reflection, with the names FML gives it.
 */
public final class Connections {
    private static volatile Members members;

    private Connections() {
    }

    /** Stands in for manager.isChannelOpen() in the tick: a manager that has no channel yet counts as open, so the tick keeps it. */
    public static boolean isOpenOrPending(Object manager) throws ReflectiveOperationException {
        Members found = members(manager.getClass());

        if (found.channel.get(manager) == null)
            return true;

        return (Boolean) invoke(found.isChannelOpen, manager);
    }

    /** Stands in for manager.processReceivedPackets() in the tick: a manager that has no channel yet waits for a later tick. */
    public static void processIfActive(Object manager) throws ReflectiveOperationException {
        Members found = members(manager.getClass());

        if (found.channel.get(manager) != null)
            invoke(found.processReceivedPackets, manager);
    }

    private static Members members(Class<?> type) throws ReflectiveOperationException {
        Members found = members;

        if (found == null || found.type != type) {
            found = new Members(type);
            members = found;
        }

        return found;
    }

    private static Object invoke(Method method, Object manager) throws IllegalAccessException {
        try {
            return method.invoke(manager);
        } catch (InvocationTargetException exception) {
            Throwable cause = exception.getCause();

            if (cause instanceof RuntimeException)
                throw (RuntimeException) cause;

            if (cause instanceof Error)
                throw (Error) cause;

            throw new IllegalStateException(cause);
        }
    }

    private static final class Members {
        final Class<?> type;
        final Field channel;
        final Method isChannelOpen;
        final Method processReceivedPackets;

        Members(Class<?> type) throws ReflectiveOperationException {
            this.type = type;
            channel = declaredField(type, "field_150746_k");
            channel.setAccessible(true);
            isChannelOpen = type.getMethod("func_150724_d");
            processReceivedPackets = type.getMethod("func_74428_b");
        }

        // The channel is private to NetworkManager, which a mod may extend.
        private static Field declaredField(Class<?> type, String name) throws NoSuchFieldException {
            for (Class<?> current = type; current != null; current = current.getSuperclass()) {
                try {
                    return current.getDeclaredField(name);
                } catch (NoSuchFieldException exception) {
                    // Look in the superclass.
                }
            }

            throw new NoSuchFieldException(name);
        }
    }
}
