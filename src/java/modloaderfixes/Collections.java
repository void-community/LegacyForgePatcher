package modloaderfixes;

import java.util.Arrays;
import java.util.Comparator;
import java.util.List;

/** Sorts a list as Java before 8u20 did, which FML for Minecraft 1.7.2 relies on while LaunchWrapper iterates the list. */
public final class Collections {
    /** Sorts a copy of the elements and sets them back, which doesn't count as a structural change to the list, so its iterators carry on. */
    @SuppressWarnings({"unchecked", "rawtypes"})
    public static void sort(List list, Comparator comparator) {
        Object[] elements = list.toArray();

        Arrays.sort(elements, comparator);
        java.util.Collections.copy(list, Arrays.asList(elements));
    }
}
