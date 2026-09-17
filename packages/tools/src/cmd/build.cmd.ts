import { Command } from '@commander-js/extra-typings'
import { cliError } from '@jahands/cli-tools'

import { getDspProjectName, validateDspArchive } from '../mods/dsp'
import { getPackageJson } from '../workspace/package-json'

export const buildCmd = new Command('build').description('Build DSP mods')

buildCmd
	.command('dotnet-dsp')
	.description(
		'Build a DSP mod .csproj in the current directory (Release) and bundle it as a Thunderstore zip in dist/'
	)
	.action(async () => {
		const name = await getDspProjectName()
		const csproj = `${name}.csproj`

		// macOS path_helper puts a bare /usr/local/share/dotnet host (no SDK) ahead of
		// mise's dotnet-root on PATH, so run the host from DOTNET_ROOT when mise sets it
		const dotnet = process.env.DOTNET_ROOT ? path.join(process.env.DOTNET_ROOT, 'dotnet') : 'dotnet'
		await $({
			stdio: 'inherit',
			verbose: true,
		})`${dotnet} build ${csproj} -c Release`

		const outDir = 'bin/Release/net472'
		const bundleDir = `dist/${name}`
		await fs.rm(bundleDir, { recursive: true, force: true })
		await fs.mkdir(bundleDir, { recursive: true })
		await Promise.all(
			[`${outDir}/${name}.dll`, 'manifest.json', 'README.md'].map((file) =>
				fs.copy(file, `${bundleDir}/${path.basename(file)}`)
			)
		)
		await fs.copy('img/icon.png', `${bundleDir}/icon.png`)
		if (await fs.exists('CHANGELOG.md')) {
			await fs.copy('CHANGELOG.md', `${bundleDir}/CHANGELOG.md`)
		}
		const { thunderstore } = await getPackageJson()
		for (const pattern of thunderstore?.includes ?? []) {
			const files = await glob(pattern, { cwd: outDir })
			if (files.length === 0) {
				throw cliError(`thunderstore.includes pattern matched nothing in ${outDir}: ${pattern}`)
			}
			await Promise.all(files.map((file) => fs.copy(`${outDir}/${file}`, `${bundleDir}/${file}`)))
		}

		await fs.rm(`dist/${name}.zip`, { force: true })
		await $({
			cwd: bundleDir,
			verbose: true,
		})`zip -qr ../${name}.zip .`
		await validateDspArchive(name)
	})
