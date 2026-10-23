package net.minecraft.network;

import net.minecraft.network.protocol.Packet;

/**
 * Encodes packets in the outgoing protocol the pipeline has, as Minecraft's does from 1.20.5: it has none between the end of configuration and play, and
 * configuration, which a server switch starts, has no id for the chunk batch answer.
 */
public class Connection {
    private volatile String outboundProtocol = "configuration";
    private volatile PacketListener listener;

    public void setupInboundProtocol(PacketListener newListener) {
        listener = newListener;
    }

    public void setupOutboundProtocol(String protocol) {
        outboundProtocol = protocol;
    }

    public boolean isConnected() {
        return true;
    }

    public PacketListener getPacketListener() {
        return listener;
    }

    public void send(Packet packet) {
        String protocol = outboundProtocol;

        if (protocol == null)
            throw new IllegalStateException("Pipeline has no outbound protocol configured, can't process packet " + packet);

        if (!protocol.equals("play"))
            throw new IllegalStateException("Sending unknown packet '" + packet.id() + "' in " + protocol);

        System.out.println("SENT " + packet + " as " + protocol + " on " + Thread.currentThread().getName());
    }
}
