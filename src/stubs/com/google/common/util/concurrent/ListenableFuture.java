package com.google.common.util.concurrent;

import java.util.concurrent.Future;

/** Guava's future, which Minecraft 1.12's scheduling methods return. */
public interface ListenableFuture<V> extends Future<V> {
}
