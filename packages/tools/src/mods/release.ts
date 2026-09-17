import { $, fs, glob, os, path } from 'zx'

import { repoRoot } from './build'

const namespace = 'Geostyx'

export async function isPublished(name: string, version: string): Promise<boolean> {
	if (!/^[a-zA-Z0-9_]+$/.test(name) || !/^\d+\.\d+\.\d+$/.test(version)) {
		throw new Error('Invalid Thunderstore name or version')
	}

	const response = await fetch(
		`https://thunderstore.io/api/experimental/package/${namespace}/${name}/${version}/`,
		{ signal: AbortSignal.timeout(30_000) }
	)

	if (response.status === 404) {
		return false
	}

	if (!response.ok) {
		throw new Error(`Thunderstore version lookup failed: ${response.status}`)
	}

	const result = (await response.json()) as {
		namespace: string
		name: string
		version_number: string
	}

	if (result.namespace !== namespace || result.name !== name || result.version_number !== version) {
		throw new Error('Unexpected Thunderstore version response')
	}

	return true
}

export async function publishMods(): Promise<void> {
	if (!process.env.TCLI_AUTH_TOKEN) {
		throw new Error('Set TCLI_AUTH_TOKEN')
	}

	const projects = await glob('mods/*/*.csproj', { cwd: repoRoot })

	if (!projects.length) {
		throw new Error('No mod projects found')
	}

	for (const project of projects.sort()) {
		const name = path.basename(project, '.csproj')
		const archive = path.join(repoRoot, path.dirname(project), 'dist', `${name}.zip`)

		const manifest = JSON.parse(
			await $({ quiet: true })`unzip -p ${archive} manifest.json`.text()
		) as {
			name: string
			version_number: string
			description: string
			website_url: string
			dependencies: string[]
		}

		if (await isPublished(manifest.name, manifest.version_number)) {
			console.log(`Already published: ${namespace}-${manifest.name}-${manifest.version_number}`)
			continue
		}

		const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'geos-thunderstore-'))

		try {
			const config = path.join(tmpDir, 'thunderstore.toml')

			await fs.writeFile(
				config,
				[
					'[config]',
					'schemaVersion = "0.0.1"',
					'[package]',
					`namespace = ${JSON.stringify(namespace)}`,
					`name = ${JSON.stringify(manifest.name)}`,
					`versionNumber = ${JSON.stringify(manifest.version_number)}`,
					`description = ${JSON.stringify(manifest.description)}`,
					`websiteUrl = ${JSON.stringify(manifest.website_url)}`,
					'containsNsfwContent = false',
					'[package.dependencies]',
					...manifest.dependencies.map((dependency) => {
						const separator = dependency.lastIndexOf('-')
						if (separator < 1) throw new Error(`Invalid dependency: ${dependency}`)
						return `${JSON.stringify(dependency.slice(0, separator))} = ${JSON.stringify(dependency.slice(separator + 1))}`
					}),
					'[publish]',
					'communities = ["dyson-sphere-program"]',
					'',
				].join('\n')
			)

			await $`tcli publish --config-path ${config} --file ${archive}`
		} finally {
			await fs.remove(tmpDir)
		}
	}
}
