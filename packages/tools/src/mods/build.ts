import { createHash } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'
import * as z from 'zod'
import { $, chalk, echo, fs, glob, os, path } from 'zx'

export const repoRoot = fileURLToPath(new URL('../../../../', import.meta.url))

const DspArchiveValidationResult = z.object({
	icon: z.string(),
	message: z.string(),
})

export async function buildDspMod(mod: string): Promise<void> {
	const projects = await glob('*.csproj', { cwd: mod })

	if (projects.length !== 1) {
		throw new Error(`Expected one mod project in ${mod}`)
	}

	const name = path.basename(projects[0], '.csproj')
	// macOS path_helper puts a bare /usr/local/share/dotnet host (no SDK) ahead of
	// mise's dotnet-root on PATH, so run the host from DOTNET_ROOT when mise sets it
	const dotnet = process.env.DOTNET_ROOT ? path.join(process.env.DOTNET_ROOT, 'dotnet') : 'dotnet'

	const validatorSource = fileURLToPath(new URL('./validate-dsp-archive.cs.txt', import.meta.url))
	const hash = createHash('sha256')
		.update(await fs.readFile(validatorSource))
		.digest('hex')
		.slice(0, 16)
	const validatorDir = path.join(os.tmpdir(), `dsp-archive-validator-${hash}`)
	const validatorDll = path.join(validatorDir, 'validate-dsp-archive.dll')

	const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'geos-dsp-build-'))

	try {
		if (!(await fs.pathExists(validatorDll))) {
			const validator = path.join(tmpDir, 'validate-dsp-archive.cs')
			await fs.copy(validatorSource, validator)

			const output = path.join(tmpDir, 'validator')
			await $`${dotnet} build ${validator} -o ${output} --artifacts-path ${path.join(tmpDir, 'artifacts')}`

			await fs.rename(output, validatorDir).catch((err: NodeJS.ErrnoException) => {
				// Another parallel mod build already populated validatorDir; use its output.
				if (err.code !== 'ENOTEMPTY' && err.code !== 'EEXIST') throw err
			})
		}

		await $({ cwd: mod })`${dotnet} build ${projects[0]} -c Release`

		const bundle = path.join(tmpDir, 'bundle')
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

		await Promise.all(
			Object.entries(files).map(([filename, source]) =>
				fs.copy(source, path.join(bundle, filename))
			)
		)

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
		const result = DspArchiveValidationResult.parse(
			await $({ quiet: true })`${dotnet} ${validatorDll} ${JSON.stringify(options)}`.json()
		)

		const icon = sharp(Buffer.from(result.icon, 'base64'), { failOn: 'warning' })
		const metadata = await icon.metadata()

		if (metadata.format !== 'png' || metadata.width !== 256 || metadata.height !== 256) {
			throw new Error('icon.png must be a 256x256 PNG')
		}

		await icon.raw().toBuffer()

		echo(chalk.green(result.message))
	} finally {
		await fs.remove(tmpDir)
	}
}
