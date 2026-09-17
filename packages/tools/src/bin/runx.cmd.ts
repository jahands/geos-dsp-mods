import { Command } from '@commander-js/extra-typings'
import { $ } from 'zx'

import { buildDspMod } from '../mods/build'
import { publishMods } from '../mods/release'

$.verbose = true

const program = new Command('runx')

program
	.command('build')
	.command('dotnet-dsp')
	.action(async () => {
		await buildDspMod(process.cwd())
	})

program
	.command('release')
	.description('Upload validated archives for unpublished versions')
	.action(publishMods)

await program.parseAsync()
