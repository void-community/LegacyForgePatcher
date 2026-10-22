package net.minecraftforge.fml.common.network.handshake;

import net.minecraft.network.EnumConnectionState;
import net.minecraft.network.NetworkManager;

/** Starts the server side of the handshake as FML for Minecraft 1.12 does: it switches the connection to the play state at once, on the server thread. */
public class NetworkDispatcher {
    private final NetworkManager manager;

    NetworkDispatcher(NetworkManager manager) {
        this.manager = manager;
    }

    int serverInitiateHandshake() {
        manager.func_150723_a(EnumConnectionState.PLAY);
        return 0;
    }

    public static void main(String[] arguments) throws InterruptedException {
        NetworkManager manager = new NetworkManager();

        manager.hold();
        manager.sendPacket("SPacketEnableCompression", EnumConnectionState.LOGIN);
        manager.sendPacket("SPacketLoginSuccess", EnumConnectionState.LOGIN);
        new NetworkDispatcher(manager).serverInitiateHandshake();
        manager.sendPacket("FML|HS ServerHello", EnumConnectionState.PLAY);
        manager.drain();

        if (!manager.failed().isEmpty()) {
            System.out.println("Can't serialize unregistered packet: " + manager.failed());
            System.exit(1);
        }

        System.out.println("SENT " + manager.sent());
    }
}
