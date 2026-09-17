import { Command } from '@commander-js/extra-typings'

import { getDspProjectName, validateDspArchive } from '../mods/dsp'

export const checkCmd = new Command('check').description('Check DSP mods')

checkCmd
	.command('dotnet-dsp')
	.description('Validate a built DSP mod archive, its contents, and compiled plugin version')
	.argument('[archive]', 'Archive path (defaults to dist/<project>.zip)')
	.action(async (archive) => {
		await validateDspArchive(await getDspProjectName(), archive)
	})
