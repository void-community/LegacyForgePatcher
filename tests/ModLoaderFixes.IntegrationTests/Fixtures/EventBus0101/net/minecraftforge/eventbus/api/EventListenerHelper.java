package net.minecraftforge.eventbus.api;

import java.util.Collections;
import java.util.IdentityHashMap;
import java.util.Map;

import net.minecraftforge.eventbus.ListenerList;

/** Builds listener lists as EventBus 0.9.3 to 0.10.1 do: inside a synchronized map's computeIfAbsent, which holds the map's monitor meanwhile. */
public class EventListenerHelper {
    private static Map<Class<?>, ListenerList> listeners = Collections.synchronizedMap(new IdentityHashMap<>());

    public static ListenerList getListenerList(Class<?> eventClass) {
        return getListenerListInternal(eventClass, false);
    }

    static ListenerList getListenerListInternal(Class<?> eventClass, boolean fromInstanceCall) {
        return listeners.computeIfAbsent(eventClass, c -> computeListenerList(eventClass, fromInstanceCall));
    }

    private static ListenerList computeListenerList(Class<?> eventClass, boolean fromInstanceCall) {
        return Builder.build(eventClass);
    }
}
