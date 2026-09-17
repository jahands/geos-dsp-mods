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
		expect(await isPublished('Geostyx', 'Example', '1.0.0')).toBe(false)
	})
	test('skips an already published exact version', async () => {
		fetchMock.mockResolvedValue(
			Response.json({
				namespace: 'Geostyx',
				name: 'Example',
				version_number: '1.0.0',
			})
		)
		expect(await isPublished('Geostyx', 'Example', '1.0.0')).toBe(true)
	})
	test('fails closed when Thunderstore is unavailable', async () => {
		fetchMock.mockResolvedValue(new Response(null, { status: 503 }))
		await expect(isPublished('Geostyx', 'Example', '1.0.0')).rejects.toThrow('503')
	})
	test('rejects unexpected version responses', async () => {
		fetchMock.mockResolvedValue(
			Response.json({
				namespace: 'Geostyx',
				name: 'Example',
				version_number: '2.0.0',
			})
		)
		await expect(isPublished('Geostyx', 'Example', '1.0.0')).rejects.toThrow('Unexpected')
	})
})
