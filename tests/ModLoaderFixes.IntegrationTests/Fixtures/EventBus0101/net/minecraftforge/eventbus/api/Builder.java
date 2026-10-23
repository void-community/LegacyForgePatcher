package net.minecraftforge.eventbus.api;

import net.minecraftforge.eventbus.ListenerList;
import net.minecraftforge.fml.common.gameevent.TickEvent;

/** Builds an event's listener list as EventBus does, by constructing the event, which initializes its class and its superclasses first. */
final class Builder {
    private Builder() {
    }

    static ListenerList build(Class<?> eventClass) {
        // The worker builds PlayerTickEvent's list once the client thread has started initializing TickEvent.
        if (eventClass == TickEvent.PlayerTickEvent.class) {
            Startup.WORKER_BUILDING.countDown();
            Startup.await(Startup.CLIENT_INITIALIZING);
        }

        try {
            eventClass.getConstructor().newInstance();
        } catch (ReflectiveOperationException exception) {
            throw new IllegalStateException(exception);
        }

        return new ListenerList();
    }
}
