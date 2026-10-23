package net.minecraft.client.multiplayer;

import net.minecraft.network.Connection;
import net.minecraft.network.protocol.Packet;

/** Minecraft's client connection listener, by Mojang's names; from 1.20.5 its superclass declares send, where the JVM finds it. */
public class ClientPacketListener {
    public void send(Packet<?> packet) {
        throw new UnsupportedOperationException();
    }

    public Connection getConnection() {
        throw new UnsupportedOperationException();
    }
}
