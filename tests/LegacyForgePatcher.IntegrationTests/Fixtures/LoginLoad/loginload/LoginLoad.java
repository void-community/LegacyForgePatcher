package loginload;

import java.io.ByteArrayOutputStream;
import java.io.DataInputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.TreeMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.zip.DataFormatException;
import java.util.zip.Inflater;

/**
 * Logs many players in to a Minecraft server in offline mode at once, as far as the first packets of the play state, and prints how the logins ended. It
 * runs beside the server, so the logins load its event loops without crossing the host's network. Given FML's network version (FML2 for Forge 1.13 to
 * 1.17, FML3 for 1.18 to 1.20.1), it logs in as a Forge client: it answers FML's handshake with the server's own mod list and acknowledges the rest.
 * Usage: LoginLoad host port protocol logins parallel [fmlVersion]
 */
public final class LoginLoad {
    private static final int LOGIN_DISCONNECT = 0x00;
    private static final int LOGIN_SUCCESS = 0x02;
    private static final int SET_COMPRESSION = 0x03;
    private static final int LOGIN_PLUGIN_REQUEST = 0x04;
    private static final int LOGIN_PLUGIN_RESPONSE = 0x02;
    private static final String LOGIN_WRAPPER = "fml:loginwrapper";
    private static final String HANDSHAKE = "fml:handshake";
    private static final int S2C_MOD_LIST = 1;
    private static final int C2S_MOD_LIST_REPLY = 2;
    private static final int S2C_CHANNEL_MISMATCH = 6;
    private static final int C2S_ACKNOWLEDGE = 99;
    private static final int PLAY_PACKETS_TO_READ = 3;
    private static final int TIMEOUT_MILLISECONDS = 30000;
    private static final int PROBE_MILLISECONDS = 60000;
    private static final String STILL_STARTING = "Server is still starting!";
    private static final String LOGGED_IN = "logged in";

    private LoginLoad() {
    }

    public static void main(String[] arguments) throws InterruptedException {
        final String host = arguments[0];
        final int port = Integer.parseInt(arguments[1]);
        final int protocol = Integer.parseInt(arguments[2]);
        int logins = Integer.parseInt(arguments[3]);
        int parallel = Integer.parseInt(arguments[4]);
        final String fmlVersion = arguments.length > 5 ? arguments[5] : null;
        final Map<String, Integer> outcomes = new TreeMap<String, Integer>();

        System.out.println("PROBE " + waitForLogins(host, port, protocol, fmlVersion));

        ExecutorService pool = Executors.newFixedThreadPool(parallel);

        for (int index = 0; index < logins; index++) {
            final String name = "player" + index;

            pool.execute(new Runnable() {
                @Override
                public void run() {
                    String outcome = login(host, port, protocol, name, fmlVersion);

                    synchronized (outcomes) {
                        Integer count = outcomes.get(outcome);
                        outcomes.put(outcome, count == null ? 1 : count + 1);
                    }
                }
            });
        }

        pool.shutdown();

        if (!pool.awaitTermination(5, TimeUnit.MINUTES))
            throw new IllegalStateException("The logins did not finish");

        System.out.println("LOGINS " + outcomes);
        System.exit(outcomes.keySet().equals(java.util.Collections.singleton(LOGGED_IN)) ? 0 : 1);
    }

    // Forge and FML log "Done" before ServerStartedEvent's handlers run, and turn players away with "Server is still starting!" until those finish, which on a
    // busy machine outlasts this client's start. One player logs in first, again until the server lets it, so that the burst only meets a server that admits
    // players. Returns how that login ended, after how many tries.
    private static String waitForLogins(String host, int port, int protocol, String fmlVersion) throws InterruptedException {
        long deadline = System.currentTimeMillis() + PROBE_MILLISECONDS;
        int tries = 0;
        String outcome;

        do {
            if (tries > 0)
                Thread.sleep(500);

            outcome = login(host, port, protocol, "probe", fmlVersion);
            tries++;
        } while (outcome.contains(STILL_STARTING) && System.currentTimeMillis() < deadline);

        return outcome + " after " + tries + (tries == 1 ? " try" : " tries");
    }

    private static String login(String host, int port, int protocol, String name, String fmlVersion) {
        Socket socket = new Socket();

        try {
            socket.connect(new InetSocketAddress(host, port), TIMEOUT_MILLISECONDS);
            socket.setSoTimeout(TIMEOUT_MILLISECONDS);

            OutputStream output = socket.getOutputStream();
            DataInputStream input = new DataInputStream(socket.getInputStream());
            ByteArrayOutputStream handshake = new ByteArrayOutputStream();

            // The handshake asks for the login state (2); then the player's name starts the login.
            writeVarInt(handshake, protocol);
            // A Forge client says which FML network version it speaks after the server address.
            writeString(handshake, fmlVersion == null ? host : host + "\0" + fmlVersion + "\0");
            handshake.write(port >> 8);
            handshake.write(port & 0xff);
            writeVarInt(handshake, 2);
            writePacket(output, -1, 0x00, handshake.toByteArray());

            ByteArrayOutputStream loginStart = new ByteArrayOutputStream();

            writeString(loginStart, name);

            // Minecraft 1.19 adds an optional signature key to Login Start, 1.19.1 an optional UUID, and 1.19.3 drops the key again.
            if (protocol >= 759 && protocol <= 760)
                loginStart.write(0);

            if (protocol >= 760)
                loginStart.write(0);

            int threshold = -1;
            int handshakeMessages = 0;

            writePacket(output, threshold, 0x00, loginStart.toByteArray());

            byte[] packet = readPacket(input, threshold);

            while (packet[0] != LOGIN_SUCCESS) {
                DataInputStream fields = new DataInputStream(new java.io.ByteArrayInputStream(packet, 1, packet.length - 1));

                // The server says why, as a JSON text component.
                if (packet[0] == LOGIN_DISCONNECT)
                    return "disconnected at login: " + readString(fields);

                if (packet[0] == SET_COMPRESSION)
                    threshold = readVarInt(fields);
                else if (packet[0] == LOGIN_PLUGIN_REQUEST) {
                    String outcome = answerLoginQuery(output, threshold, fields);

                    if (outcome == null)
                        handshakeMessages++;
                    else if (!outcome.isEmpty())
                        return outcome;
                } else
                    return "unexpected login packet " + packet[0];

                packet = readPacket(input, threshold);
            }

            for (int read = 0; read < PLAY_PACKETS_TO_READ; read++)
                readPacket(input, threshold);

            // A server that let the client in as vanilla never ran the handshake this client is meant to exercise.
            return fmlVersion == null || handshakeMessages > 0 ? LOGGED_IN : "logged in without FML's handshake";
        } catch (EOFException exception) {
            return "connection closed";
        } catch (IOException exception) {
            return "connection failed: " + exception.getMessage();
        } catch (DataFormatException exception) {
            return "malformed packet";
        } finally {
            try {
                socket.close();
            } catch (IOException exception) {
                // The login has ended either way.
            }
        }
    }

    // Answers one login query: FML's handshake messages as a Forge client with the server's mods does, anything else as a vanilla client does. Returns null
    // for an FML message answered, an empty string for another query, and how the login ended when the server refused it.
    private static String answerLoginQuery(OutputStream output, int threshold, DataInputStream query) throws IOException {
        int messageId = readVarInt(query);
        String channel = readString(query);
        ByteArrayOutputStream response = new ByteArrayOutputStream();

        writeVarInt(response, messageId);

        if (!channel.equals(LOGIN_WRAPPER)) {
            response.write(0);
            writePacket(output, threshold, LOGIN_PLUGIN_RESPONSE, response.toByteArray());
            return "";
        }

        readString(query);
        readVarInt(query);

        int index = query.readUnsignedByte();
        ByteArrayOutputStream message = new ByteArrayOutputStream();

        if (index == S2C_CHANNEL_MISMATCH)
            return "channel mismatch";

        if (index == S2C_MOD_LIST) {
            message.write(C2S_MOD_LIST_REPLY);
            copyStrings(query, message);

            int channels = readVarInt(query);

            writeVarInt(message, channels);

            for (int entry = 0; entry < channels; entry++) {
                writeString(message, readString(query));
                writeString(message, readString(query));
            }

            int registries = readVarInt(query);

            writeVarInt(message, registries);

            for (int entry = 0; entry < registries; entry++) {
                writeString(message, readString(query));
                writeString(message, "");
            }
        } else
            message.write(C2S_ACKNOWLEDGE);

        response.write(1);
        writeString(response, HANDSHAKE);
        writeVarInt(response, message.size());
        message.writeTo(response);
        writePacket(output, threshold, LOGIN_PLUGIN_RESPONSE, response.toByteArray());

        return null;
    }

    private static void copyStrings(DataInputStream input, OutputStream output) throws IOException {
        int count = readVarInt(input);

        writeVarInt(output, count);

        for (int entry = 0; entry < count; entry++)
            writeString(output, readString(input));
    }

    private static String readString(DataInputStream input) throws IOException {
        byte[] text = new byte[readVarInt(input)];

        input.readFully(text);

        return new String(text, StandardCharsets.UTF_8);
    }

    private static void writeVarInt(OutputStream output, int value) throws IOException {
        int remaining = value;

        do {
            int current = remaining & 0x7f;

            remaining >>>= 7;
            output.write(remaining != 0 ? current | 0x80 : current);
        } while (remaining != 0);
    }

    private static void writeString(OutputStream output, String value) throws IOException {
        byte[] text = value.getBytes(StandardCharsets.UTF_8);

        writeVarInt(output, text.length);
        output.write(text);
    }

    // Once compression is set, a data length comes first; zero says the rest isn't compressed, which every packet sent here is small enough for.
    private static void writePacket(OutputStream output, int threshold, int id, byte[] fields) throws IOException {
        ByteArrayOutputStream body = new ByteArrayOutputStream();
        ByteArrayOutputStream frame = new ByteArrayOutputStream();

        if (threshold >= 0)
            body.write(0);

        writeVarInt(body, id);
        body.write(fields);
        writeVarInt(frame, body.size());
        body.writeTo(frame);
        frame.writeTo(output);
        output.flush();
    }

    private static int readVarInt(DataInputStream input) throws IOException {
        int value = 0;

        for (int index = 0; index < 5; index++) {
            int current = input.readUnsignedByte();

            value |= (current & 0x7f) << (7 * index);

            if ((current & 0x80) == 0)
                return value;
        }

        throw new IOException("malformed VarInt");
    }

    // Returns the packet's id, which fits in one byte for every packet read here, followed by its data. Before compression is set a packet is its id and
    // data; after, a data length comes first, and the id and data are zlib-compressed when it isn't zero.
    private static byte[] readPacket(DataInputStream input, int threshold) throws IOException, DataFormatException {
        byte[] frame = new byte[readVarInt(input)];

        input.readFully(frame);

        if (threshold < 0)
            return frame;

        DataInputStream framed = new DataInputStream(new java.io.ByteArrayInputStream(frame));
        int dataLength = readVarInt(framed);
        byte[] rest = new byte[framed.available()];

        framed.readFully(rest);

        if (dataLength == 0)
            return rest;

        Inflater inflater = new Inflater();
        byte[] packet = new byte[dataLength];

        try {
            inflater.setInput(rest);

            if (inflater.inflate(packet) != dataLength)
                throw new DataFormatException("short packet");
        } finally {
            inflater.end();
        }

        return packet;
    }
}
