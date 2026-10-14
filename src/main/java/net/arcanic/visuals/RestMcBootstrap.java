/*
 * This file is part of Arcanic Visuals (https://github.com/arcanic-su/arcanic)
 *
 * Copyright (c) 2026 Arcanic
 *
 * Arcanic Visuals is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Arcanic Visuals is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Arcanic Visuals. If not, see <https://www.gnu.org/licenses/>.
 */
package net.arcanic.visuals;

import net.fabricmc.api.ClientModInitializer;

/** RestMC bootstrap that selects the RestMC-owned account and sync service before the original client initializes. */
public final class RestMcBootstrap implements ClientModInitializer {
    public static final String API = "https://mc.tiers.rest";

    @Override
    public void onInitializeClient() {
        // Sync reads this property when its class is first initialized. Force the production RestMC service.
        System.setProperty("arcanic.api", API);

        // Keep the upstream internals stable while exposing a RestMC-native token override for our launcher/dev tools.
        String restToken = System.getenv("RESTMC_TOKEN");
        if (restToken != null && !restToken.isBlank()) {
            System.setProperty("arcanic.token", restToken);
        } else {
            String propertyToken = System.getProperty("restmc.token", "");
            if (!propertyToken.isBlank()) System.setProperty("arcanic.token", propertyToken);
        }
    }
}
