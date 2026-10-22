package net.minecraft.client;

import net.minecraft.util.thread.BlockableEventLoop;

/** The client, whose event loop is the client thread. */
public class Minecraft extends BlockableEventLoop {
    private static final Minecraft INSTANCE = new Minecraft();

    public static Minecraft getInstance() {
        return INSTANCE;
    }
}
