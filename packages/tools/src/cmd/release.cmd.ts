import { Command } from '@commander-js/extra-typings'

import { publishMods } from '../mods/thunderstore'

export const releaseCmd = new Command('release')
	.description('Upload validated archives for versions missing from Thunderstore')
	.action(publishMods)
