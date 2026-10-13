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

import net.arcanic.visuals.gui.ArcanicMenu;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.Constant;
import org.spongepowered.asm.mixin.injection.ModifyConstant;

/** Directly patches the visible brand in the in-game client menu. */
@Mixin(ArcanicMenu.class)
public abstract class RestMcMenuBrandingMixin {
    @ModifyConstant(method = {"<init>", "brand"}, constant = @Constant(stringValue = "Arcanic"), require = 0)
    private String restmc$name(String ignored) {
        return "RestMC";
    }
}
