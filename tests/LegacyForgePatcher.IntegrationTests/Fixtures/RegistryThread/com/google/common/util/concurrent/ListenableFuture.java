package com.google.common.util.concurrent;

import java.util.concurrent.Future;

/** The part of Guava's ListenableFuture that Minecraft's scheduled tasks return. */
public interface ListenableFuture<V> extends Future<V> {
}
