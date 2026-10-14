/*
 * This file is part of Arcanic Visuals (https://github.com/arcanic-su/arcanic)
 *
 * Copyright (c) 2026 Arcanic
 *
 * Arcanic Visuals is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
package net.arcanic.visuals.mixin;

import net.arcanic.visuals.gui.FrontEndMenu;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.Constant;
import org.spongepowered.asm.mixin.injection.ModifyConstant;

/** Directly patches title-screen branding so visible upstream names cannot leak through. */
@Mixin(FrontEndMenu.class)
public abstract class RestMcFrontEndBrandingMixin {
    @ModifyConstant(method = {"<init>", "render", "accountHint"}, constant = @Constant(stringValue = "Arcanic"), require = 0)
    private String restmc$name(String ignored) {
        return "RestMC";
    }

    @ModifyConstant(method = "<init>", constant = @Constant(stringValue = "Arcanic Social"), require = 0)
    private String restmc$socialEntry(String ignored) {
        return "RestMC Social";
    }

    @ModifyConstant(method = "render", constant = @Constant(stringValue = "Arcanic Visuals 1.6.7"), require = 0)
    private String restmc$footer(String ignored) {
        return "RestMC 1.6.7";
    }
}
