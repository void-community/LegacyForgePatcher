package net.minecraftforge.eventbus.api;

import java.util.Map;
import java.util.concurrent.locks.ReadWriteLock;

import net.minecraftforge.eventbus.ListenerList;

/** EventBus 0.10.2 to 0.10.4's EventListenerHelper, whose members here are private static there; EventListenerLists opens them. */
public class EventListenerHelper {
    public static Map<Class<?>, ListenerList> listeners;
    public static ReadWriteLock lock;

    public static ListenerList computeListenerList(Class<?> eventClass, boolean fromInstanceCall) {
        throw new UnsupportedOperationException();
    }
}
