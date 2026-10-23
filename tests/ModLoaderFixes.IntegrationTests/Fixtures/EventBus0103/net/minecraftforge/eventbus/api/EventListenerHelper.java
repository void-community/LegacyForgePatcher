package net.minecraftforge.eventbus.api;

import java.util.IdentityHashMap;
import java.util.Map;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReadWriteLock;
import java.util.concurrent.locks.ReentrantReadWriteLock;

import net.minecraftforge.eventbus.ListenerList;

/** Builds listener lists as EventBus 0.10.2 to 0.10.4 do: while it holds the write lock of a fair read-write lock. */
public class EventListenerHelper {
    private static final Map<Class<?>, ListenerList> listeners = new IdentityHashMap<>();
    private static final ReadWriteLock lock = new ReentrantReadWriteLock(true);

    public static ListenerList getListenerList(Class<?> eventClass) {
        return getListenerListInternal(eventClass, false);
    }

    static ListenerList getListenerListInternal(Class<?> eventClass, boolean fromInstanceCall) {
        final Lock readLock = lock.readLock();
        readLock.lock();
        ListenerList listenerList = listeners.get(eventClass);
        readLock.unlock();

        if (listenerList == null) {
            final Lock writeLock = lock.writeLock();
            writeLock.lock();
            readLock.lock();
            listenerList = listeners.get(eventClass);

            if (listenerList == null) {
                listenerList = computeListenerList(eventClass, fromInstanceCall);
                listeners.put(eventClass, listenerList);
            }

            readLock.unlock();
            writeLock.unlock();
        }

        return listenerList;
    }

    private static ListenerList computeListenerList(Class<?> eventClass, boolean fromInstanceCall) {
        return Builder.build(eventClass);
    }
}
