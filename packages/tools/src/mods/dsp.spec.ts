import { fileURLToPath } from 'node:url'
import { crc32, deflateSync } from 'node:zlib'
import { fmt } from 'llm-tools'
import { assert, test as baseTest, describe, expect, vi } from 'vitest'

import { validateDspIcon } from './dsp'

import type { DspArchiveValidationOptions, ThunderstoreManifest } from './dsp'

const dotnet = process.env.DOTNET_ROOT ? path.join(process.env.DOTNET_ROOT, 'dotnet') : 'dotnet'
const manifest: ThunderstoreManifest = {
	name: 'ExampleMod',
	version_number: '1.2.3',
	description: 'Example mod',
	website_url: '',
	dependencies: ['xiaoye97-BepInEx-5.4.17'],
}

function png(width = 256, height = 256, compressedData?: Buffer): Buffer {
	const header = Buffer.alloc(13)
	header.writeUInt32BE(width, 0)
	header.writeUInt32BE(height, 4)
	header[8] = 8
	header[9] = 2

	const chunks: Buffer[] = [Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])]

	for (const [name, data] of [
		['IHDR', header],
		['IDAT', compressedData ?? deflateSync(Buffer.alloc(height * (width * 3 + 1)))],
		['IEND', Buffer.alloc(0)],
	] as const) {
		const payload = Buffer.concat([Buffer.from(name), data])

		const length = Buffer.alloc(4)
		length.writeUInt32BE(data.length)

		const checksum = Buffer.alloc(4)
		checksum.writeUInt32BE(crc32(payload))

		chunks.push(length, payload, checksum)
	}

	return Buffer.concat(chunks)
}

type Validate = (
	files: Record<string, Buffer>,
	options?: {
		pluginGuid?: string | null
		allowedFiles?: string[]
		extraEntry?: { Name: string; Data: string; Attributes?: number }
	}
) => Promise<ProcessOutput>

const it = baseTest.extend<{
	build: { directory: string; plugin: Buffer }
	archive: string
	entries: () => Record<string, Buffer>
	validate: Validate
}>({
	build: [
		async ({ task: _task }, provide) => {
			const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'dsp-archive-test-'))

			await fs.copy(
				fileURLToPath(new URL('./validate-dsp-archive.cs.txt', import.meta.url)),
				path.join(directory, 'validator.cs')
			)

			await fs.writeFile(
				path.join(directory, 'fixture.cs'),
				fmt.trim(`
					#:property PublishAot=false

					using System.IO.Compression;
					using System.Text.Json;

					var entries = JsonSerializer.Deserialize<Entry[]>(args[1])!;
					using var archive = ZipFile.Open(args[0], ZipArchiveMode.Create);

					foreach (var entry in entries)
					{
						var item = archive.CreateEntry(entry.Name);

						if (entry.Attributes != null)
						{
							item.ExternalAttributes = entry.Attributes.Value;
						}

						using var stream = item.Open();
						stream.Write(Convert.FromBase64String(entry.Data));
					}

					record Entry(string Name, string Data, int? Attributes);

					[BepInEx.BepInPlugin("example.main", "Display name differs", "1.2.3")]
					class MainPlugin { }

					[BepInEx.BepInPlugin("example.helper", "Helper", "9.0.0")]
					class HelperPlugin { }

					namespace BepInEx
					{
						public class BepInPlugin : Attribute
						{
							public BepInPlugin(string guid, string name, string version)
								=> throw new Exception("Do not execute plugin attributes");
						}
					}
				`)
			)

			await Promise.all(
				['validator', 'fixture'].map((name) => {
					const args = [
						'build',
						path.join(directory, `${name}.cs`),
						'-o',
						path.join(directory, name),
						'--artifacts-path',
						path.join(directory, `${name}-artifacts`),
					]

					return $`${dotnet} ${args}`.quiet()
				})
			)

			await provide({
				directory,
				plugin: await fs.readFile(path.join(directory, 'fixture/fixture.dll')),
			})
			await fs.remove(directory)
		},
		{ scope: 'file' },
	],
	archive: async ({ build }, provide) => {
		const directory = await fs.mkdtemp(path.join(build.directory, 'archive-'))
		await provide(path.join(directory, 'archive.zip'))
		await fs.remove(directory)
	},
	entries: async ({ build }, provide) => {
		await provide(() => ({
			'manifest.json': Buffer.from(JSON.stringify(manifest)),
			'README.md': Buffer.from('# Example'),
			'icon.png': png(),
			'Example.dll': build.plugin,
		}))
	},
	validate: async ({ build, archive }, provide) => {
		await provide(async (files, options = {}) => {
			await fs.remove(archive)

			const contents = Object.entries(files).map(([Name, data]) => ({
				Name,
				Data: data.toString('base64'),
			}))

			if (options.extraEntry) {
				contents.push(options.extraEntry)
			}

			const fixture = [
				path.join(build.directory, 'fixture/fixture.dll'),
				archive,
				JSON.stringify(contents),
			]
			await $`${dotnet} ${fixture}`.quiet()

			const validationOptions: DspArchiveValidationOptions = {
				ArchivePath: archive,
				PluginName: 'Example',
				PluginGuid: options.pluginGuid === undefined ? 'example.main' : options.pluginGuid,
				AllowedFiles: options.allowedFiles ?? [
					'manifest.json',
					'README.md',
					'icon.png',
					'Example.dll',
				],
			}

			const validator = [
				path.join(build.directory, 'validator/validator.dll'),
				JSON.stringify(validationOptions),
			]
			return await $({ nothrow: true, quiet: true })`${dotnet} ${validator}`
		})
	},
})

describe.concurrent('DSP archive validation', { timeout: 60_000 }, () => {
	it('reads compiled plugin attributes without executing them and accepts a declared native/runtime dependency', async ({
		entries,
		validate,
	}) => {
		const files = entries()
		files['x64/helper.dll'] = Buffer.from('native runtime dependency')
		files['System.Runtime.CompilerServices.Unsafe.dll'] = Buffer.from('declared runtime dependency')

		const result = await validate(files, { allowedFiles: Object.keys(files) })

		expect(result.stderr).toBe('')
		expect(result.exitCode).toBe(0)

		expect(result.stdout).toContain('example.main 1.2.3')
	})

	it('accepts extra manifest fields and a 250-character Unicode description', async ({
		entries,
		validate,
	}) => {
		const files = entries()
		files['manifest.json'] = Buffer.from(
			JSON.stringify({ ...manifest, description: '😀'.repeat(250), gameVersion: 'example' })
		)

		expect((await validate(files)).exitCode).toBe(0)
	})

	it.for(['manifest.json', 'README.md', 'icon.png', 'Example.dll'])(
		'rejects missing %s',
		async (name, { entries, validate }) => {
			const files = entries()
			delete files[name]

			expect((await validate(files)).stderr).toContain(`Missing archive file: ${name}`)
		}
	)

	it.for([
		['name', 'bad-name', 'Invalid manifest name'],
		['version_number', '1.2', 'major.minor.patch'],
		['description', 'x'.repeat(251), '250 characters'],
		['website_url', 'not a URL', 'HTTP(S) URL'],
		['dependencies', ['plugin.guid'], 'Invalid Thunderstore dependency'],
	] as const)(
		'rejects invalid manifest %s',
		async ([field, value, diagnostic], { entries, validate }) => {
			const files = entries()
			files['manifest.json'] = Buffer.from(JSON.stringify({ ...manifest, [field]: value }))

			const result = await validate(files)

			expect(result.exitCode).toBe(1)
			expect(result.stderr).toContain(diagnostic)
		}
	)

	it('rejects invalid UTF-8 in documentation', async ({ entries, validate }) => {
		const files = entries()
		files['README.md'] = Buffer.from([0xff])

		expect((await validate(files)).exitCode).toBe(1)
	})

	it('decodes valid icons and rejects wrong dimensions, truncation, and invalid pixel data', async ({
		onTestFinished,
	}) => {
		const exit = vi.spyOn(process, 'exit').mockImplementation(() => {
			assert.fail('CLI exited')
		})
		onTestFinished(() => exit.mockRestore())

		await expect(validateDspIcon(png())).resolves.toBeUndefined()
		await expect(validateDspIcon(png(128, 256))).rejects.toThrow('CLI exited')
		expect(exit).toHaveBeenCalledWith(1)
		await expect(validateDspIcon(png().subarray(0, 40))).rejects.toThrow()
		await expect(validateDspIcon(png(256, 256, Buffer.alloc(0)))).rejects.toThrow()
		await expect(
			validateDspIcon(png(256, 256, Buffer.from('invalid compressed pixels')))
		).rejects.toThrow()
	})

	it('accepts a UTF-8 BOM manifest', async ({ entries, validate }) => {
		const files = entries()
		files['manifest.json'] = Buffer.concat([
			Buffer.from([0xef, 0xbb, 0xbf]),
			files['manifest.json'],
		])

		expect((await validate(files)).exitCode).toBe(0)
	})

	it.for([
		'Assembly-CSharp.dll',
		'Assembly-CSharp-publicized.dll',
		'UnityEngine.CoreModule.dll',
		'BepInEx.dll',
		'0Harmony.dll',
		'MonoMod.RuntimeDetour.dll',
	])('rejects %s even if explicitly included', async (name, { entries, validate }) => {
		const files = entries()
		files[`nested/${name}`] = Buffer.from('unexpected loader/game assembly')

		expect((await validate(files, { allowedFiles: Object.keys(files) })).stderr).toContain(
			'Game or loader-provided assembly'
		)
	})

	it('rejects undeclared files', async ({ entries, validate }) => {
		const files = entries()
		files['secret.json'] = Buffer.from('{}')

		expect((await validate(files)).stderr).toContain('Unexpected archive file')
	})

	it.for(['../outside.json', '/absolute.json', 'nested\\file.json'])(
		'rejects unsafe path %s',
		async (name, { entries, validate }) => {
			const files = entries()
			files[name] = Buffer.from('{}')

			expect((await validate(files, { allowedFiles: Object.keys(files) })).stderr).toContain(
				'Unsafe archive path'
			)
		}
	)

	it('rejects duplicate entries and symlinks', async ({ entries, validate }) => {
		expect(
			(await validate(entries(), { extraEntry: { Name: 'README.md', Data: '' } })).stderr
		).toContain('Duplicate archive path')

		expect(
			(
				await validate(entries(), {
					extraEntry: { Name: 'link', Data: '', Attributes: 0xa1ff << 16 },
				})
			).stderr
		).toContain('Symlink in archive')
	})

	it('requires primary-plugin selection for a multi-plugin DLL', async ({ entries, validate }) => {
		expect((await validate(entries(), { pluginGuid: null })).stderr).toContain(
			'set thunderstore.pluginGuid'
		)

		expect((await validate(entries(), { pluginGuid: 'absent' })).stderr).toContain('found 0')
	})

	it('rejects a plugin/manifest version mismatch', async ({ entries, validate }) => {
		const files = entries()
		files['manifest.json'] = Buffer.from(JSON.stringify({ ...manifest, version_number: '1.2.4' }))

		expect((await validate(files)).stderr).toContain(
			'version 1.2.3 does not match manifest version 1.2.4'
		)
	})

	it('rejects a corrupt plugin DLL', async ({ entries, validate }) => {
		const files = entries()
		files['Example.dll'] = Buffer.from('not a DLL')

		expect((await validate(files)).exitCode).toBe(1)
	})

	it('rechecks an archive through runx and rejects an undecodable icon', async ({
		archive,
		entries,
		validate,
	}) => {
		const project = path.dirname(archive)

		const [bun, sdks] = await Promise.all([
			$`bun -p process.execPath`.quiet().text(),
			$`${dotnet} --list-sdks`.quiet().text(),
			fs.writeFile(path.join(project, 'Example.csproj'), '<Project Sdk="Microsoft.NET.Sdk" />'),
			fs.writeFile(
				path.join(project, 'package.json'),
				JSON.stringify({ name: 'example', thunderstore: { pluginGuid: 'example.main' } })
			),
		])
		const sdkDirectory = sdks
			.split('\n')
			.find((line) => line.startsWith('10.'))
			?.match(/\[(.+)\]/)?.[1]

		expect(sdkDirectory).toBeDefined()

		const args = [
			fileURLToPath(new URL('../bin/runx.cmd.ts', import.meta.url)),
			'check',
			'dotnet-dsp',
			path.basename(archive),
		]

		const $$ = $({
			nothrow: true,
			quiet: true,
			cwd: project,
			env: { ...process.env, DOTNET_ROOT: path.dirname(sdkDirectory!) },
		})

		await validate(entries())
		const valid = await $$`${bun.trim()} ${args}`

		expect(valid.stderr).toBe('')
		expect(valid.exitCode).toBe(0)

		expect(valid.stdout).toContain('Validated')
		const files = entries()
		files['icon.png'] = png(256, 256, Buffer.alloc(0))
		await validate(files)

		const invalid = await $$`${bun.trim()} ${args}`

		expect(invalid.exitCode).not.toBe(0)
		expect(invalid.stdout).not.toContain('Validated')
	})
})
