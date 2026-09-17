import { afterEach, describe, expect, spyOn, test } from 'bun:test'

import { isPublished } from './release'

const originalFetch = globalThis.fetch
const fetchMock = spyOn(globalThis, 'fetch')

afterEach(() => {
	fetchMock.mockReset()
	fetchMock.mockImplementation(originalFetch)
})

describe('Thunderstore version lookup', () => {
	test('only a 404 means the version needs publishing', async () => {
		fetchMock.mockResolvedValue(new Response(null, { status: 404 }))

		expect(await isPublished('Example', '1.0.0')).toBe(false)
	})

	test('skips an already published version', async () => {
		fetchMock.mockResolvedValue(Response.json({}))

		expect(await isPublished('Example', '1.0.0')).toBe(true)
	})

	test('fails closed when Thunderstore is unavailable', async () => {
		fetchMock.mockResolvedValue(new Response(null, { status: 503 }))

		await expect(isPublished('Example', '1.0.0')).rejects.toThrow('503')
	})
})
