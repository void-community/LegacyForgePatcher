package cpw.mods.modlauncher;

import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;
import java.util.stream.Stream;

/**
 * The class hierarchy cache of ModLauncher 2 to 5's TransformerClassWriter: three static HashMaps that every thread transforming a class shares, where recording a
 * class puts its parent before its hierarchy and its interface flag. Its main method lets one thread record a class's parent and hold there while another asks for
 * the common superclass of that class, as ASM does when it computes the frames of a method; ModLauncher then reads the hierarchy as null and throws.
 */
public class TransformerClassWriter {
    private static final HashMap<String, String> classParents = new HashMap<>();
    private static final HashMap<String, Set<String>> classHierarchies = new HashMap<>();
    private static final HashMap<String, Boolean> isInterface = new HashMap<>();

    // The classes the transformer finds as resources: each with its superclass.
    private static final Map<String, String> SUPERCLASSES = new HashMap<>();
    private static final String TARGET = "net/minecraft/world/WorldServer";
    private static final CountDownLatch PARENT_RECORDED = new CountDownLatch(1);
    private static final CountDownLatch READER_DONE = new CountDownLatch(1);
    private static volatile boolean holding = true;

    static {
        SUPERCLASSES.put("java/lang/Object", null);
        SUPERCLASSES.put("net/minecraft/world/World", "java/lang/Object");
        SUPERCLASSES.put(TARGET, "net/minecraft/world/World");
    }

    private Set<String> getSupers(String typeName) {
        if (!classParents.containsKey(typeName))
            computeHierarchy(typeName);

        return classHierarchies.get(typeName);
    }

    // The SuperCollectingVisitor's visit, for a class read from its resource.
    private void computeHierarchy(String name) {
        String superName = SUPERCLASSES.get(name);

        classParents.put(name, superName);

        // The first time the target is recorded, its thread holds here, between its parent and its hierarchy, until the other thread has asked about it.
        if (name.equals(TARGET) && holding) {
            holding = false;
            PARENT_RECORDED.countDown();
            await(READER_DONE);
        }

        if (superName != null) {
            computeHierarchy(superName);
            classHierarchies.put(name, Stream.concat(Stream.of(name), classHierarchies.get(superName).stream()).collect(Collectors.toSet()));
        } else {
            classHierarchies.put(name, Collections.singleton("java/lang/Object"));
        }

        isInterface.put(name, false);
    }

    protected String getCommonSuperClass(String type1, String type2) {
        if (getSupers(type2).contains(type1))
            return type1;

        if (getSupers(type1).contains(type2))
            return type2;

        return "java/lang/Object";
    }

    public static void main(String[] arguments) throws InterruptedException {
        final TransformerClassWriter writer = new TransformerClassWriter();
        final String[] answer = new String[1];
        final Throwable[] failure = new Throwable[1];
        Thread recording = new Thread(new Runnable() {
            @Override
            public void run() {
                writer.getSupers(TARGET);
            }
        }, "modloading-worker-1");
        Thread asking = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    await(PARENT_RECORDED);
                    answer[0] = writer.getCommonSuperClass(TARGET, "net/minecraft/world/World");
                } catch (RuntimeException exception) {
                    failure[0] = exception;
                } finally {
                    READER_DONE.countDown();
                }
            }
        }, "Server thread");

        recording.start();
        asking.start();
        recording.join();
        asking.join();

        if (failure[0] != null) {
            System.out.println("CRASHED " + failure[0]);
            System.exit(1);
        }

        System.out.println("COMMON " + answer[0]);
        System.exit("net/minecraft/world/World".equals(answer[0]) ? 0 : 1);
    }

    private static void await(CountDownLatch latch) {
        try {
            if (!latch.await(10, TimeUnit.SECONDS))
                throw new IllegalStateException("The other thread never got there");
        } catch (InterruptedException exception) {
            throw new IllegalStateException(exception);
        }
    }
}
