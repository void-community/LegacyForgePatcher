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
 * Logs many players in to a Minecraft 1.12 server in offline mode at once, as far as the first packets of the play state, and prints how the logins
 * ended. It runs beside the server, so the logins load its event loops without crossing the host's network.
 * Usage: LoginLoad host port protocol logins parallel
 */
public final class LoginLoad {
    private static final int LOGIN_DISCONNECT = 0x00;
    private static final int LOGIN_SUCCESS = 0x02;
    private static final int SET_COMPRESSION = 0x03;
    private static final int PLAY_PACKETS_TO_READ = 3;
    private static final int TIMEOUT_MILLISECONDS = 30000;
    private static final String LOGGED_IN = "logged in";

    private LoginLoad() {
    }

    public static void main(String[] arguments) throws InterruptedException {
        final String host = arguments[0];
        final int port = Integer.parseInt(arguments[1]);
        final int protocol = Integer.parseInt(arguments[2]);
        int logins = Integer.parseInt(arguments[3]);
        int parallel = Integer.parseInt(arguments[4]);
        final Map<String, Integer> outcomes = new TreeMap<String, Integer>();
        ExecutorService pool = Executors.newFixedThreadPool(parallel);

        for (int index = 0; index < logins; index++) {
            final String name = "player" + index;

            pool.execute(new Runnable() {
                @Override
                public void run() {
                    String outcome = login(host, port, protocol, name);

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

    private static String login(String host, int port, int protocol, String name) {
        Socket socket = new Socket();

        try {
            socket.connect(new InetSocketAddress(host, port), TIMEOUT_MILLISECONDS);
            socket.setSoTimeout(TIMEOUT_MILLISECONDS);

            OutputStream output = socket.getOutputStream();
            DataInputStream input = new DataInputStream(socket.getInputStream());
            ByteArrayOutputStream handshake = new ByteArrayOutputStream();

            // The handshake asks for the login state (2); then the player's name starts the login.
            writeVarInt(handshake, protocol);
            writeString(handshake, host);
            handshake.write(port >> 8);
            handshake.write(port & 0xff);
            writeVarInt(handshake, 2);
            writePacket(output, 0x00, handshake.toByteArray());

            ByteArrayOutputStream loginStart = new ByteArrayOutputStream();

            writeString(loginStart, name);
            writePacket(output, 0x00, loginStart.toByteArray());

            int threshold = -1;
            byte[] packet = readPacket(input, threshold);

            while (packet[0] != LOGIN_SUCCESS) {
                if (packet[0] == LOGIN_DISCONNECT)
                    return "disconnected at login";

                if (packet[0] != SET_COMPRESSION)
                    return "unexpected login packet " + packet[0];

                threshold = readVarInt(new DataInputStream(new java.io.ByteArrayInputStream(packet, 1, packet.length - 1)));
                packet = readPacket(input, threshold);
            }

            for (int read = 0; read < PLAY_PACKETS_TO_READ; read++)
                readPacket(input, threshold);

            return LOGGED_IN;
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

    private static void writePacket(OutputStream output, int id, byte[] fields) throws IOException {
        ByteArrayOutputStream body = new ByteArrayOutputStream();
        ByteArrayOutputStream frame = new ByteArrayOutputStream();

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
