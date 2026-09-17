import 'zx/globals'

import { program } from '@commander-js/extra-typings'
import { catchProcessError } from '@jahands/cli-tools'

import { buildCmd } from '../cmd/build.cmd'
import { checkCmd } from '../cmd/check.cmd'
import { releaseCmd } from '../cmd/release.cmd'

program
	.name('runx')
	.description("A CLI for scripts that automate Geo's DSP mods")

	.addCommand(buildCmd)
	.addCommand(checkCmd)
	.addCommand(releaseCmd)

	// don't hang for unresolved promises
	.hook('postAction', () => process.exit(0))
	.parseAsync()
	.catch(catchProcessError())
