package net.minecraftforge.fml.common.network.handshake;

import com.google.common.collect.Multimap;

import java.util.Collections;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraftforge.registries.GameData;

/**
 * The client side of FML's handshake as Forge for Minecraft 1.12 and 1.12.1 have it: the network thread rebuilds the registries in WAITINGSERVERCOMPLETE and
 * DONE. The states are declared in Forge's order, so their bodies compile to the same classes: WAITINGSERVERCOMPLETE to $4 and DONE to $7.
 */
public enum FMLHandshakeClientState {
    START {
    },
    HELLO {
    },
    WAITINGSERVERDATA {
    },
    WAITINGSERVERCOMPLETE {
        @Override
        public void accept(Map<String, String> snapshot) {
            Multimap<String, String> missing = GameData.injectSnapshot(snapshot, false, false);

            System.out.println("MISSING " + missing.size());
        }
    },
    PENDINGCOMPLETE {
    },
    COMPLETE {
    },
    DONE {
        @Override
        public void accept(Map<String, String> snapshot) {
            GameData.revertToFrozen();
        }
    },
    ERROR {
    };

    public void accept(Map<String, String> snapshot) {
    }

    /** A proxy resets the handshake and replays it with another server, which the network thread handles while the client thread ticks. */
    public static void main(String[] arguments) throws InterruptedException {
        Minecraft minecraft = Minecraft.func_71410_x();
        Thread network = new Thread(new Runnable() {
            @Override
            public void run() {
                DONE.accept(Collections.<String, String>emptyMap());
                WAITINGSERVERCOMPLETE.accept(Collections.singletonMap("minecraft:block", "server"));
            }
        }, "Netty Client IO #1");

        network.start();
        minecraft.tickUntil(network);
    }
}
