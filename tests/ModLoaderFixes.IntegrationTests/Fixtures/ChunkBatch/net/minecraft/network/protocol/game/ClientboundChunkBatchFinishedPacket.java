package net.minecraft.network.protocol.game;

import net.minecraft.network.protocol.Packet;

/** The server's end of a chunk batch. */
public final class ClientboundChunkBatchFinishedPacket implements Packet {
    @Override
    public String id() {
        return "clientbound/minecraft:chunk_batch_finished";
    }
}
