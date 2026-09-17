import { PackageJson } from './package-json.schema'

export * from './package-json.schema'

/**
 * Read package.json in the current directory
 */
export async function getPackageJson(): Promise<PackageJson> {
	return PackageJson.parse(await Bun.file('./package.json').json())
}
