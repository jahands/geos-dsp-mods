import { createHash } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'
import { $, fs, glob, os, path } from 'zx'

export const repoRoot = fileURLToPath(new URL('../../../../', import.meta.url))

export async function buildDspMod(mod: string): Promise<void> {
	const projects = await glob('*.csproj', { cwd: mod })

	if (projects.length !== 1) {
		throw new Error(`Expected one mod project in ${mod}`)
	}

	const name = path.basename(projects[0], '.csproj')
	const dotnet = process.env.DOTNET_ROOT ? path.join(process.env.DOTNET_ROOT, 'dotnet') : 'dotnet'

	const validatorSource = fileURLToPath(new URL('./validate-dsp-archive.cs.txt', import.meta.url))
	const hash = createHash('sha256')
		.update(await fs.readFile(validatorSource))
		.digest('hex')
		.slice(0, 16)
	const validatorDir = path.join(os.tmpdir(), `geos-dsp-validator-${hash}`)
	const validatorDll = path.join(validatorDir, 'validate-dsp-archive.dll')

	const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'geos-dsp-build-'))

	try {
		if (!(await fs.pathExists(validatorDll))) {
			const validator = path.join(tmpDir, 'validate-dsp-archive.cs')
			await fs.copy(validatorSource, validator)

			const output = path.join(tmpDir, 'validator')
			await $`${dotnet} build ${validator} -o ${output} --artifacts-path ${path.join(tmpDir, 'artifacts')}`

			await fs.rename(output, validatorDir).catch((err: NodeJS.ErrnoException) => {
				if (err.code !== 'ENOTEMPTY' && err.code !== 'EEXIST') throw err
			})
		}

		await $({ cwd: mod })`${dotnet} restore ${projects[0]} --locked-mode`
		await $({
			cwd: mod,
		})`${dotnet} build ${projects[0]} -c Release --no-restore`

		const bundle = path.join(tmpDir, 'bundle')
		await fs.ensureDir(bundle)

		const files: Record<string, string> = {
			[`${name}.dll`]: path.join(mod, `bin/Release/net472/${name}.dll`),
			'manifest.json': path.join(mod, 'manifest.json'),
			'README.md': path.join(mod, 'README.md'),
			'icon.png': path.join(mod, 'img/icon.png'),
			LICENSE: path.join(repoRoot, 'LICENSE'),
		}

		if (await fs.pathExists(path.join(mod, 'CHANGELOG.md'))) {
			files['CHANGELOG.md'] = path.join(mod, 'CHANGELOG.md')
		}

		for (const [filename, source] of Object.entries(files)) {
			await fs.copy(source, path.join(bundle, filename))
		}

		const icon = sharp(files['icon.png'], { failOn: 'warning' })
		const metadata = await icon.metadata()

		if (metadata.format !== 'png' || metadata.width !== 256 || metadata.height !== 256) {
			throw new Error('icon.png must be a 256x256 PNG')
		}

		await icon.raw().toBuffer()

		const archive = path.join(mod, 'dist', `${name}.zip`)
		await fs.ensureDir(path.dirname(archive))
		await fs.remove(archive)
		await $({ cwd: bundle })`zip -q ${archive} ${Object.keys(files)}`

		const options = {
			ArchivePath: archive,
			PluginName: name,
			PluginGuid: null,
			AllowedFiles: Object.keys(files),
		}
		await $({
			quiet: true,
		})`${dotnet} ${validatorDll} ${JSON.stringify(options)}`
		console.log(`Validated ${archive}`)
	} finally {
		await fs.remove(tmpDir)
	}
}
