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

import net.arcanic.visuals.hud.DynamicIsland;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.Constant;
import org.spongepowered.asm.mixin.injection.ModifyConstant;

/** Keeps the Dynamic Island watermark on the RestMC brand. */
@Mixin(DynamicIsland.class)
public abstract class RestMcIslandBrandingMixin {
    @ModifyConstant(method = {"<init>", "render"}, constant = @Constant(stringValue = "Arcanic"), require = 0)
    private String restmc$name(String ignored) {
        return "RestMC";
    }
}
