package legacyforgepatcher;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Future;

/**
 * Runs Forge's registry rebuilds on the Minecraft client thread, as Forge for Minecraft 1.12.2 does, instead of on the network thread that handles the
 * handshake while the client thread ticks the world. It lives in the bootstrap loader, so its callers pass a class of Forge's loader, through which it
 * reaches GameData and Minecraft by reflection.
 */
public final class ClientThread {
    private ClientThread() {
    }

    /** Schedules GameData.revertToFrozen() on the client thread, without waiting for it, as Forge for 1.12.2 does. */
    public static void revertToFrozen(Class<?> forgeClass) throws ReflectiveOperationException {
        final Method revertToFrozen = gameData(forgeClass).getMethod("revertToFrozen");

        schedule(forgeClass, "func_152344_a", Runnable.class, new Runnable() {
            @Override
            public void run() {
                invoke(revertToFrozen, null);
            }
        });
    }

    /** Runs GameData.injectSnapshot(snapshot, injectFrozenData, isLocalWorld) on the client thread and returns its result, as Forge for 1.12.2 does. */
    public static Object injectSnapshot(Class<?> forgeClass, final Map<?, ?> snapshot, final boolean injectFrozenData, final boolean isLocalWorld) throws ReflectiveOperationException {
        final Method injectSnapshot = gameData(forgeClass).getMethod("injectSnapshot", Map.class, boolean.class, boolean.class);
        Future<?> result = schedule(forgeClass, "func_152343_a", Callable.class, new Callable<Object>() {
            @Override
            public Object call() {
                return invoke(injectSnapshot, null, snapshot, injectFrozenData, isLocalWorld);
            }
        });

        return await(result);
    }

    private static Class<?> gameData(Class<?> forgeClass) throws ClassNotFoundException {
        return Class.forName("net.minecraftforge.registries.GameData", false, forgeClass.getClassLoader());
    }

    // Minecraft.getMinecraft().addScheduledTask(task), by the names FML's deobfuscation gives them: it runs the task at once on the client thread and
    // queues it from any other.
    private static Future<?> schedule(Class<?> forgeClass, String addScheduledTask, Class<?> taskType, Object task) throws ReflectiveOperationException {
        Class<?> minecraft = Class.forName("net.minecraft.client.Minecraft", false, forgeClass.getClassLoader());
        Object instance = minecraft.getMethod("func_71410_x").invoke(null);

        return (Future<?>) minecraft.getMethod(addScheduledTask, taskType).invoke(instance, task);
    }

    // Waits as Guava's Futures.getUnchecked does, through interrupts, and throws what the task threw, as the call would have on its own thread.
    private static Object await(Future<?> result) {
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
            throw rethrow(exception.getCause());
        } finally {
            if (interrupted)
                Thread.currentThread().interrupt();
        }
    }

    private static Object invoke(Method method, Object target, Object... arguments) {
        try {
            return method.invoke(target, arguments);
        } catch (InvocationTargetException exception) {
            throw rethrow(exception.getCause());
        } catch (IllegalAccessException exception) {
            throw new IllegalStateException(exception);
        }
    }

    private static RuntimeException rethrow(Throwable cause) {
        if (cause instanceof RuntimeException)
            throw (RuntimeException) cause;

        if (cause instanceof Error)
            throw (Error) cause;

        throw new IllegalStateException(cause);
    }
}
