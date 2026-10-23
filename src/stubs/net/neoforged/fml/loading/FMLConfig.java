package net.neoforged.fml.loading;

import java.nio.file.Path;

/** NeoForge's FML config, with the methods ModLoaderConfigurationLoad adds to it: configData and loadFrom are private there. */
public class FMLConfig {
    /** configData, as an Object, since NeoForge's FML 4 changed its type. */
    public Object modloaderfixes$configData() {
        throw new UnsupportedOperationException();
    }

    /** loadFrom. */
    public void modloaderfixes$loadFrom(Path file) {
        throw new UnsupportedOperationException();
    }
}
