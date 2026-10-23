package net.minecraft.client;

import java.util.concurrent.Callable;
import java.util.concurrent.Executor;

import com.google.common.util.concurrent.ListenableFuture;

/** Minecraft's client: by FML's names as Forge 1.12 runs it, and by Mojang's from 1.20.5, where it is an Executor that runs tasks on the client thread. */
public abstract class Minecraft implements Executor {
    /** getMinecraft. */
    public static Minecraft func_71410_x() {
        throw new UnsupportedOperationException();
    }

    /** addScheduledTask. */
    public ListenableFuture<Object> func_152344_a(Runnable task) {
        throw new UnsupportedOperationException();
    }

    /** callFromMainThread. */
    public <V> ListenableFuture<V> func_152343_a(Callable<V> task) {
        throw new UnsupportedOperationException();
    }

    public static Minecraft getInstance() {
        throw new UnsupportedOperationException();
    }
}
