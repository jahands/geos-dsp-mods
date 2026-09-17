import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, assert, beforeAll, describe, expect, it, vi } from 'vitest'

import { isPublished } from './thunderstore'

const server = setupServer()

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

const url = 'https://thunderstore.io/api/experimental/package/Geostyx/Example/1.0.0/'

describe('isPublished', () => {
	it('only a 404 means the version needs publishing', async () => {
		server.use(http.get(url, () => new HttpResponse(null, { status: 404 })))

		expect(await isPublished('Example', '1.0.0')).toBe(false)
	})

	it('skips an already published version', async () => {
		server.use(http.get(url, () => HttpResponse.json({})))

		expect(await isPublished('Example', '1.0.0')).toBe(true)
	})

	it('fails closed when Thunderstore is unavailable', async ({ onTestFinished }) => {
		server.use(http.get(url, () => new HttpResponse(null, { status: 503 })))
		const exit = vi.spyOn(process, 'exit').mockImplementation(() => {
			assert.fail('CLI exited')
		})
		onTestFinished(() => exit.mockRestore())

		await expect(isPublished('Example', '1.0.0')).rejects.toThrow('CLI exited')
		expect(exit).toHaveBeenCalledWith(1)
	})
})
