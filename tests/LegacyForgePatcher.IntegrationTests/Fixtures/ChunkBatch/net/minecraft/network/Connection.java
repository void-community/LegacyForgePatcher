package net.minecraft.network;

import net.minecraft.network.protocol.Packet;

/** Encodes packets in the outgoing protocol the pipeline has, and has none between the end of configuration and play, as Minecraft's does from 1.20.5. */
public class Connection {
    private volatile String outboundProtocol = "configuration";

    public void setupOutboundProtocol(String protocol) {
        outboundProtocol = protocol;
    }

    public void send(Packet packet) {
        String protocol = outboundProtocol;

        if (protocol == null)
            throw new IllegalStateException("Pipeline has no outbound protocol configured, can't process packet " + packet);

        System.out.println("SENT " + packet + " as " + protocol + " on " + Thread.currentThread().getName());
    }
}
