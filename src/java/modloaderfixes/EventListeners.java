package modloaderfixes;

import java.util.Map;
import java.util.concurrent.locks.ReadWriteLock;
import java.util.function.Function;

import net.minecraftforge.eventbus.ListenerList;
import net.minecraftforge.eventbus.api.EventListenerHelper;

/**
 * Builds EventBus's listener lists without holding the lock on its map, as EventBus 0.10.5 does. Building a list constructs an instance of the event class,
 * whose static initializer asks for a listener list too, so building it under the lock can deadlock with a thread that initializes the class. Two threads
 * that build the same list keep the first one put in the map. It is defined in EventListenerHelper's loader.
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
     * one there meanwhile, which getListenerListInternal then puts back and returns. The lock, the map and computeListenerList are private, so the patch adds methods
     * that reach them.
     */
    public static ListenerList computeListenerList(Class<?> eventClass, boolean fromInstanceCall) {
        ReadWriteLock lock = EventListenerHelper.modloaderfixes$lock();
        ListenerList built;

        lock.readLock().unlock();
        lock.writeLock().unlock();

        try {
            built = EventListenerHelper.modloaderfixes$computeListenerList(eventClass, fromInstanceCall);
        } finally {
            lock.writeLock().lock();
            lock.readLock().lock();
        }

        ListenerList existing = EventListenerHelper.modloaderfixes$listeners().get(eventClass);

        return existing != null ? existing : built;
    }
}
