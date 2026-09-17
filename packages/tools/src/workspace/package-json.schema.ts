import * as z from 'zod'

export type ThunderstoreConfig = z.infer<typeof ThunderstoreConfig>
export const ThunderstoreConfig = z.object({
	includes: z
		.array(z.string().min(1))
		.min(1)
		.describe(
			'Globs (relative to the dotnet build output dir) of extra files to ship in the Thunderstore zip next to the mod dll'
		)
		.optional(),
	pluginGuid: z
		.string()
		.min(1)
		.optional()
		.describe('Primary BepInPlugin GUID when the mod assembly contains multiple plugins'),
})

export type PackageJson = z.infer<typeof PackageJson>
export const PackageJson = z
	.object({
		name: z.string().trim().min(1),
		version: z.string().min(1),
		thunderstore: ThunderstoreConfig.optional(),
	})
	.loose()
