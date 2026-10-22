package cpw.mods.fml.relauncher;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.Iterator;
import java.util.List;

/** Sorts tweakers as FML for Minecraft 1.7.2 does: while LaunchWrapper iterates them. */
public class CoreModManager {
    private static final List<String> tweakers = new ArrayList<String>(Arrays.asList("deobfuscation", "coremods", "injection"));

    public static void sortTweakList() {
        Collections.sort(tweakers, new Comparator<String>() {
            @Override
            public int compare(String first, String second) {
                return first.compareTo(second);
            }
        });
    }

    public static void main(String[] arguments) {
        List<String> called = new ArrayList<String>();

        for (Iterator<String> iterator = tweakers.iterator(); iterator.hasNext(); ) {
            called.add(iterator.next());
            sortTweakList();
            iterator.remove();
        }

        System.out.println("CALLED " + called);
    }
}
