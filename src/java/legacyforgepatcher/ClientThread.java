package legacyforgepatcher;

import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Future;

import com.google.common.collect.Multimap;
import net.minecraft.client.Minecraft;
import net.minecraftforge.registries.GameData;

/**
 * Runs Forge's registry rebuilds on the Minecraft client thread, as Forge for Minecraft 1.12.2 does, instead of on the network thread that handles the
 * handshake while the client thread ticks the world. It is defined in the loader of FML's handshake states, the classes it is called from, and calls
 * Minecraft by the names FML gives it.
 */
public final class ClientThread {
    private ClientThread() {
    }

    /** Schedules GameData.revertToFrozen() on the client thread, without waiting for it, as Forge for 1.12.2 does. */
    public static void revertToFrozen() {
        Minecraft.func_71410_x().func_152344_a(new Runnable() {
            @Override
            public void run() {
                GameData.revertToFrozen();
            }
        });
    }

    /** Runs GameData.injectSnapshot(snapshot, injectFrozenData, isLocalWorld) on the client thread and returns its result, as Forge for 1.12.2 does. */
    public static Multimap<?, ?> injectSnapshot(final Map<?, ?> snapshot, final boolean injectFrozenData, final boolean isLocalWorld) {
        return await(Minecraft.func_71410_x().func_152343_a(new Callable<Multimap<?, ?>>() {
            @Override
            public Multimap<?, ?> call() {
                return GameData.injectSnapshot(snapshot, injectFrozenData, isLocalWorld);
            }
        }));
    }

    // Waits as Guava's Futures.getUnchecked does, through interrupts, and throws what the task threw, as the call would have on its own thread.
    private static <V> V await(Future<V> result) {
        boolean interrupted = false;

        try {
            while (true) {
                try {
                    return result.get();
                } catch (InterruptedException exception) {
                    interrupted = true;
                }
            }
        } catch (ExecutionException exception) {
            Throwable cause = exception.getCause();

            if (cause instanceof RuntimeException)
                throw (RuntimeException) cause;

            if (cause instanceof Error)
                throw (Error) cause;

            throw new IllegalStateException(cause);
        } finally {
            if (interrupted)
                Thread.currentThread().interrupt();
        }
    }
}
