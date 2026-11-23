// Fetch the exact upstream method bodies used by the scenario benchmark.
// Run with Node 22+ in (or pointing to) the benchmark directory before dotnet build.
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const commit = '9b3431a7d6cb6754b9bd0401bc2c9b024445e0c9';
const upstream = 'https://raw.githubusercontent.com/assimalign/cohesion/' + commit + '/';
const output = name => fileURLToPath(new URL(name, import.meta.url));
const storage = 'resources/Database/Assimalign.Cohesion.Database.Storage/src/';
const sqlProject = 'resources/Database/Assimalign.Cohesion.Database.Sql/src/';
async function readUpstream(path) {
    const response = await fetch(upstream + path);
    assert(response.ok, 'Could not read ' + path + ': ' + response.status);
    return response.text();
}
function extractMethod(source, declaration) {
    const start = source.indexOf(declaration);
    assert(start >= 0 && source.lastIndexOf(declaration) === start, 'Unknown or ambiguous method: ' + declaration);
    const open = source.indexOf('{', start);
    let depth = 1;
    let i = open + 1;
    for (; i < source.length && depth !== 0; i++) {
        if (source[i] === '{') depth++;
        if (source[i] === '}') depth--;
    }
    assert.equal(depth, 0);
    return source.slice(start, i);
}
function extractConstant(source, name) {
    const matches = source.match(new RegExp('^\\s*(?:public|internal|private) const int ' + name + ' = .*?;', 'gm'));
    assert.equal(matches?.length, 1, name);
    return '    ' + matches[0].trim();
}
const [page, codec, sql, license] = await Promise.all([
    readUpstream(storage + 'Units/Page.cs'),
    readUpstream(storage + 'Internal/Pages/PageImageCodec.cs'),
    readUpstream(sqlProject + 'Internal/Provisioning/SqlSchemaProvisioner.JointSpace.cs'),
    readUpstream('LICENSE')
]);
const parts = [
    '// Extracted methods and constants from Assimalign Cohesion, commit ' + commit + '.',
    '// Copyright (c) 2024 Assimalign Cohesion. MIT license, see COHESION-LICENSE.txt.',
    'internal static class Page', '{',
    ...['Size', 'ChecksumFieldOffset', 'LsnFieldOffset'].map(n => extractConstant(page, n)),
    '}', 'internal static class PageImageCodec', '{',
    ...['MaskedStart', 'MaskedEnd'].map(n => extractConstant(codec, n)),
    extractMethod(codec, '    internal static int FirstDifference('),
    '}', 'internal static class SqlSchemaProvisioner', '{',
    '    internal static int Measure(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) => JointRunBytes(left, right);',
    extractMethod(sql, '    private static int JointRunBytes('),
    '}', ''
];
await writeFile(output('COHESION-LICENSE.txt'), license);
await writeFile(output('Cohesion.cs'), parts.join('\n'));
