import * as find from 'empathic/find'
import memoizeOne from 'memoize-one'
import { path } from 'zx'

export const getRepoRoot = memoizeOne(() => {
	const pnpmLock = find.up('pnpm-lock.yaml', { cwd: import.meta.dirname })
	if (!pnpmLock) {
		throw new Error(`unable to locate pnpm-lock.yaml from ${import.meta.dirname}`)
	}
	return path.dirname(pnpmLock)
})
