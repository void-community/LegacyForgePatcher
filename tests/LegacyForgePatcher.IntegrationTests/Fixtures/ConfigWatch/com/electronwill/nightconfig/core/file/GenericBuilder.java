package com.electronwill.nightconfig.core.file;

/** Builds a file config as NightConfig's builder does; autosave and autoreload set a flag and return the builder. */
public class GenericBuilder {
    private boolean autosave;
    private boolean autoreload;

    public GenericBuilder autosave() {
        autosave = true;
        return this;
    }

    public GenericBuilder autoreload() {
        autoreload = true;
        return this;
    }

    public CommentedFileConfig build() {
        return new CommentedFileConfig(autosave, autoreload);
    }
}
