import { cliError, getEnv } from '@jahands/cli-tools'
import * as z from 'zod'

import { getRepoRoot } from '../core/repo-paths'

const namespace = 'Geostyx'

const Manifest = z.object({
	name: z.string(),
	version_number: z.string(),
})

export async function isPublished(name: string, version: string): Promise<boolean> {
	const response = await fetch(
		`https://thunderstore.io/api/experimental/package/${namespace}/${name}/${version}/`,
		{ signal: AbortSignal.timeout(30_000) }
	)

	if (response.status === 404) {
		return false
	}

	if (!response.ok) {
		throw cliError(`Thunderstore version lookup failed: ${response.status}`)
	}

	return true
}

export async function publishMods(): Promise<void> {
	getEnv('TCLI_AUTH_TOKEN')

	const repoRoot = getRepoRoot()
	const projects = (await glob('mods/*/*.csproj', { cwd: repoRoot })).sort()

	if (projects.length === 0) {
		throw cliError('No mod projects found')
	}

	for (const project of projects) {
		const mod = path.join(repoRoot, path.dirname(project))
		const name = path.basename(project, '.csproj')
		const archive = path.join(mod, 'dist', `${name}.zip`)

		if (!(await fs.pathExists(archive))) {
			throw cliError(
				`Missing ${archive}: add ${path.dirname(project)}/package.json so turbo builds it`
			)
		}

		const manifest = Manifest.parse(await fs.readJson(path.join(mod, 'manifest.json')))

		if (await isPublished(manifest.name, manifest.version_number)) {
			echo(
				chalk.blue(`Already published: ${namespace}-${manifest.name}-${manifest.version_number}`)
			)
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
					`namespace = "${namespace}"`,
					`name = "${manifest.name}"`,
					`versionNumber = "${manifest.version_number}"`,
					'[publish]',
					'communities = ["dyson-sphere-program"]',
					'',
				].join('\n')
			)

			await $({ verbose: true })`tcli publish --config-path ${config} --file ${archive}`
		} finally {
			await fs.remove(tmpDir)
		}
	}
}
