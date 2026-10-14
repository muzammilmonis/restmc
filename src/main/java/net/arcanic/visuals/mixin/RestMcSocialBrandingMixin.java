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

import net.arcanic.visuals.gui.SocialScreen;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.Constant;
import org.spongepowered.asm.mixin.injection.ModifyConstant;

/** Directly patches the visible account-card brand. */
@Mixin(SocialScreen.class)
public abstract class RestMcSocialBrandingMixin {
    @ModifyConstant(method = {"<init>", "render"}, constant = @Constant(stringValue = "Arcanic Social"), require = 0)
    private String restmc$social(String ignored) {
        return "RestMC Social";
    }
}
