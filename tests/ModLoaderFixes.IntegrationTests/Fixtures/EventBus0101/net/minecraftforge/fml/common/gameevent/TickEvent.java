package net.minecraftforge.fml.common.gameevent;

import net.minecraftforge.eventbus.ListenerList;
import net.minecraftforge.eventbus.api.Event;
import net.minecraftforge.eventbus.api.EventListenerHelper;
import net.minecraftforge.eventbus.api.Startup;

/** An event whose class initializer asks EventBus for its listener list, as EventBus's generated event classes do. */
public class TickEvent extends Event {
    private static final ListenerList LISTENERS;

    static {
        // The client thread initializes TickEvent on its first tick while a mod-loading worker builds the listener list of a subclass.
        Startup.CLIENT_INITIALIZING.countDown();
        Startup.await(Startup.WORKER_BUILDING);
        LISTENERS = EventListenerHelper.getListenerList(TickEvent.class);
    }

    public static ListenerList listeners() {
        return LISTENERS;
    }

    /** A subclass whose list a mod-loading worker builds by constructing it, which needs TickEvent initialized first. */
    public static class PlayerTickEvent extends TickEvent {
    }
}
