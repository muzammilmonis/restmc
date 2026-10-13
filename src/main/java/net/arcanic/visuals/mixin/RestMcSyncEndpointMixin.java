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

import net.arcanic.visuals.core.Sync;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.Constant;
import org.spongepowered.asm.mixin.injection.ModifyConstant;

/** Ensures the RestMC build never falls back to the upstream Arcanic account service. */
@Mixin(Sync.class)
public abstract class RestMcSyncEndpointMixin {
    @ModifyConstant(method = "<clinit>", constant = @Constant(stringValue = "https://arcanic.su"), require = 1)
    private static String restmc$apiDefault(String ignored) {
        return "https://mc.tiers.rest";
    }
}
