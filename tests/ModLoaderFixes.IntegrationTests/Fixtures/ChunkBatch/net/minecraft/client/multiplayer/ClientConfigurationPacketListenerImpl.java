package net.minecraft.client.multiplayer;

import net.minecraft.network.Connection;
import net.minecraft.network.PacketListener;

/** The listener that takes the connection's packets while the server configures the client. */
public class ClientConfigurationPacketListenerImpl extends ClientCommonPacketListenerImpl implements PacketListener {
    public ClientConfigurationPacketListenerImpl(Connection connection) {
        super(connection);
    }
}
