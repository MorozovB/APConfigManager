// Single source of truth for the app version is <Version> in the repo-root
// Directory.Build.props (which also stamps the .NET assemblies). This script
// keeps the Electron package.json in sync with it, and lets CI verify a release
// tag against it.
//
//   node version.mjs sync          stamp package.json version from the props
//   node version.mjs check <ref>   fail if <ref> (e.g. v1.2.3) != props version
import { readFileSync, writeFileSync } from 'node:fs';

const propsUrl = new URL('../../Directory.Build.props', import.meta.url);
const pkgUrl = new URL('./package.json', import.meta.url);

function propsVersion() {
    const props = readFileSync(propsUrl, 'utf8');
    const m = props.match(/<Version>\s*([^<\s]+)\s*<\/Version>/);
    if (!m) {
        console.error('version: <Version> not found in Directory.Build.props');
        process.exit(1);
    }
    return m[1];
}

const [cmd, arg] = process.argv.slice(2);
const version = propsVersion();

if (cmd === 'check') {
    const tag = (arg ?? '').replace(/^v/, '');
    if (tag !== version) {
        console.error(`version: tag "${arg}" does not match Directory.Build.props <Version> ${version}`);
        process.exit(1);
    }
    console.log(`version: tag matches ${version}`);
} else {
    const pkg = JSON.parse(readFileSync(pkgUrl, 'utf8'));
    if (pkg.version !== version) {
        pkg.version = version;
        writeFileSync(pkgUrl, JSON.stringify(pkg, null, 2) + '\n');
        console.log(`version: package.json -> ${version}`);
    } else {
        console.log(`version: package.json already ${version}`);
    }
}