package net.minecraft.network;

/** Minecraft's connection, by Mojang's names, as clients from 1.20.5 have it. */
public class Connection {
    public boolean isConnected() {
        throw new UnsupportedOperationException();
    }

    /** The listener the connection hands its packets to: a play listener, or a configuration one while the server reconfigures the client. */
    public PacketListener getPacketListener() {
        throw new UnsupportedOperationException();
    }
}
