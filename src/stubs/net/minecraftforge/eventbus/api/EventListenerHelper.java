package net.minecraftforge.eventbus.api;

import java.util.Map;
import java.util.concurrent.locks.ReadWriteLock;

import net.minecraftforge.eventbus.ListenerList;

/** EventBus 0.10.2 to 0.10.4's EventListenerHelper, with the methods EventListenerLists adds to it: lock, listeners and computeListenerList are private there. */
public class EventListenerHelper {
    public static ReadWriteLock legacyforgepatcher$lock() {
        throw new UnsupportedOperationException();
    }

    public static Map<Class<?>, ListenerList> legacyforgepatcher$listeners() {
        throw new UnsupportedOperationException();
    }

    public static ListenerList legacyforgepatcher$computeListenerList(Class<?> eventClass, boolean fromInstanceCall) {
        throw new UnsupportedOperationException();
    }
}
