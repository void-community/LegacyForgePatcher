package net.minecraft.network.protocol.game;

import net.minecraft.network.protocol.Packet;

/** The client's answer to a chunk batch. */
public final class ServerboundChunkBatchReceivedPacket implements Packet {
    private final float desiredChunksPerTick;

    public ServerboundChunkBatchReceivedPacket(float desiredChunksPerTick) {
        this.desiredChunksPerTick = desiredChunksPerTick;
    }

    @Override
    public String toString() {
        return "ServerboundChunkBatchReceivedPacket[desiredChunksPerTick=" + desiredChunksPerTick + "]";
    }
}
