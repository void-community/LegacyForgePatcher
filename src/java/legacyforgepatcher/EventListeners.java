package legacyforgepatcher;

import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.Map;
import java.util.concurrent.locks.ReadWriteLock;
import java.util.function.Function;

/**
 * Builds EventBus's listener lists without holding the lock on its map, as EventBus 0.10.5 does. Building a list constructs an instance of the event class,
 * whose static initializer asks for a listener list too, so building it under the lock can deadlock with a thread that initializes the class. Two threads
 * that build the same list keep the first one put in the map. It lives in the bootstrap loader and reaches EventListenerHelper through a class of its loader.
 */
public final class EventListeners {
    private EventListeners() {
    }

    /** Replaces map.computeIfAbsent(key, compute) on EventBus 0.9.3 to 0.10.1's synchronized map, which holds the map's monitor while it computes. */
    @SuppressWarnings({"unchecked", "rawtypes"})
    public static Object computeIfAbsent(Map map, Object key, Function compute) {
        Object value = map.get(key);

        if (value != null)
            return value;

        map.putIfAbsent(key, compute.apply(key));

        return map.get(key);
    }

    /**
     * Replaces computeListenerList(eventClass, fromInstanceCall) in EventBus 0.10.2 to 0.10.4's getListenerListInternal, which calls it holding the write lock and
     * then the read lock. It lets both go while it builds the list, takes them back in the same order, and returns the list in the map when another thread put
     * one there meanwhile, which getListenerListInternal then puts back and returns.
     */
    public static Object computeListenerList(Class<?> eventBusClass, Class<?> eventClass, boolean fromInstanceCall) throws ReflectiveOperationException {
        Class<?> helper = Class.forName("net.minecraftforge.eventbus.api.EventListenerHelper", false, eventBusClass.getClassLoader());
        ReadWriteLock lock = (ReadWriteLock) accessible(helper.getDeclaredField("lock")).get(null);
        Map<?, ?> listeners = (Map<?, ?>) accessible(helper.getDeclaredField("listeners")).get(null);
        Method computeListenerList = helper.getDeclaredMethod("computeListenerList", Class.class, boolean.class);
        Object built;

        computeListenerList.setAccessible(true);
        lock.readLock().unlock();
        lock.writeLock().unlock();

        try {
            built = computeListenerList.invoke(null, eventClass, fromInstanceCall);
        } catch (InvocationTargetException exception) {
            throw rethrow(exception.getCause());
        } finally {
            lock.writeLock().lock();
            lock.readLock().lock();
        }

        Object existing = listeners.get(eventClass);

        return existing != null ? existing : built;
    }

    private static Field accessible(Field field) {
        field.setAccessible(true);

        return field;
    }

    private static RuntimeException rethrow(Throwable cause) {
        if (cause instanceof RuntimeException)
            throw (RuntimeException) cause;

        if (cause instanceof Error)
            throw (Error) cause;

        throw new IllegalStateException(cause);
    }
}
