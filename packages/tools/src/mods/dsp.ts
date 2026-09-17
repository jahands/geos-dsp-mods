import { createHash } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import { cliError } from '@jahands/cli-tools'
import sharp from 'sharp'
import * as z from 'zod'

import { getPackageJson } from '../workspace/package-json'

export type DspArchiveValidationOptions = {
	ArchivePath: string
	PluginName: string
	PluginGuid: string | null
	AllowedFiles: string[]
}

export type ThunderstoreManifest = {
	name: string
	version_number: string
	description: string
	website_url: string
	dependencies: string[]
}

const DspArchiveValidationResult = z.object({
	icon: z.string(),
	message: z.string(),
})

export async function getDspProjectName(): Promise<string> {
	const projects = z
		.array(z.string())
		.length(1, 'expected exactly one .csproj in the current directory')
		.parse(await glob('*.csproj'))

	return path.basename(projects[0], '.csproj')
}

export async function validateDspArchive(
	name: string,
	archivePath = `dist/${name}.zip`
): Promise<void> {
	const { thunderstore } = await getPackageJson()
	const allowedFiles = [`${name}.dll`, 'manifest.json', 'README.md', 'icon.png']

	if (await fs.exists('CHANGELOG.md')) {
		allowedFiles.push('CHANGELOG.md')
	}

	for (const pattern of thunderstore?.includes ?? []) {
		const files = await glob(pattern, { cwd: 'bin/Release/net472' })

		if (files.length === 0) {
			throw cliError(`thunderstore.includes pattern matched nothing: ${pattern}`)
		}

		allowedFiles.push(...files)
	}

	const options: DspArchiveValidationOptions = {
		ArchivePath: path.resolve(archivePath),
		PluginName: name,
		PluginGuid: thunderstore?.pluginGuid ?? null,
		AllowedFiles: allowedFiles,
	}

	const dotnet = process.env.DOTNET_ROOT ? path.join(process.env.DOTNET_ROOT, 'dotnet') : 'dotnet'
	const source = fileURLToPath(new URL('./validate-dsp-archive.cs.txt', import.meta.url))
	const hash = createHash('sha256')
		.update(await fs.readFile(source))
		.digest('hex')
		.slice(0, 16)
	const validatorDir = path.join(os.tmpdir(), `dsp-archive-validator-${hash}`)
	const validatorDll = path.join(validatorDir, 'validate-dsp-archive.dll')

	if (!(await fs.pathExists(validatorDll))) {
		const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'dsp-archive-'))

		try {
			// Mod projects would compile a .cs file here through their node_modules symlinks.
			const validator = path.join(tmpDir, 'validate-dsp-archive.cs')
			await fs.copy(source, validator)

			const output = path.join(tmpDir, 'output')
			const args = [
				'build',
				validator,
				'-o',
				output,
				'--artifacts-path',
				path.join(tmpDir, 'artifacts'),
			] satisfies string[]

			await $({
				stdio: ['ignore', 'pipe', 'inherit'],
			})`${dotnet} ${args}`

			await fs.rename(output, validatorDir).catch((e: NodeJS.ErrnoException) => {
				// Another parallel mod build already populated validatorDir; use its output.
				if (e.code !== 'ENOTEMPTY' && e.code !== 'EEXIST') {
					throw e
				}
			})
		} finally {
			await fs.rm(tmpDir, { recursive: true, force: true })
		}
	}

	const result = DspArchiveValidationResult.parse(
		await $({
			stdio: ['ignore', 'pipe', 'inherit'],
		})`${dotnet} ${validatorDll} ${JSON.stringify(options)}`.json()
	)

	await validateDspIcon(Buffer.from(result.icon, 'base64'))

	echo(chalk.green(result.message))
}

export async function validateDspIcon(bytes: Buffer): Promise<void> {
	const icon = sharp(bytes, { failOn: 'warning' })
	const metadata = await icon.metadata()

	if (metadata.format !== 'png' || metadata.width !== 256 || metadata.height !== 256) {
		throw cliError('icon.png must be a 256x256 PNG')
	}

	await icon.raw().toBuffer()
}
