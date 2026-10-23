package net.minecraft.client.multiplayer;

import net.minecraft.network.Connection;
import net.minecraft.network.protocol.Packet;

/** Sends packets on the connection, as Minecraft's client listeners do. */
public class ClientCommonPacketListenerImpl {
    protected final Connection connection;

    protected ClientCommonPacketListenerImpl(Connection connection) {
        this.connection = connection;
    }

    public void send(Packet packet) {
        connection.send(packet);
    }

    public Connection getConnection() {
        return connection;
    }
}
