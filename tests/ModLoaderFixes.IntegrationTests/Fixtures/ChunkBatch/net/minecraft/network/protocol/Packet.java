package net.minecraft.network.protocol;

/** A packet, as Minecraft names its interface, with the id its protocol knows it by. */
public interface Packet {
    String id();
}
