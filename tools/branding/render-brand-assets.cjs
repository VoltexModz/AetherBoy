const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const repositoryRoot = path.resolve(__dirname, '..', '..');
const brandingRoot = path.join(repositoryRoot, 'branding');
const exportRoot = path.join(brandingRoot, 'exports');
const applicationBrandingRoot = path.join(repositoryRoot, 'nanoboy', 'Branding');
// User-approved artwork. Only resize/encode; do not redraw, crop or recolor it.
const masterSource = path.join(brandingRoot, 'aetherboy-logo-2026-10-02.jpg');
const iconSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

function createIco(frames) {
  const headerSize = 6;
  const entrySize = 16;
  let imageOffset = headerSize + entrySize * frames.length;
  const header = Buffer.alloc(headerSize);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(frames.length, 4);

  const entries = [];
  for (const frame of frames) {
    const entry = Buffer.alloc(entrySize);
    entry.writeUInt8(frame.size === 256 ? 0 : frame.size, 0);
    entry.writeUInt8(frame.size === 256 ? 0 : frame.size, 1);
    entry.writeUInt8(0, 2);
    entry.writeUInt8(0, 3);
    entry.writeUInt16LE(1, 4);
    entry.writeUInt16LE(32, 6);
    entry.writeUInt32LE(frame.data.length, 8);
    entry.writeUInt32LE(imageOffset, 12);
    entries.push(entry);
    imageOffset += frame.data.length;
  }

  return Buffer.concat([header, ...entries, ...frames.map((frame) => frame.data)]);
}

async function renderPng(sourcePath, size) {
  return sharp(sourcePath, { density: 384 })
    .resize(size, size, { fit: 'contain' })
    .png({ compressionLevel: 9, palette: false })
    .toBuffer();
}

async function main() {
  fs.mkdirSync(exportRoot, { recursive: true });
  fs.mkdirSync(applicationBrandingRoot, { recursive: true });

  const frames = [];
  for (const size of iconSizes) {
    const data = await renderPng(masterSource, size);
    fs.writeFileSync(path.join(exportRoot, `aetherboy-mark-${size}.png`), data);
    frames.push({ size, data });
  }

  const applicationArtwork = await renderPng(masterSource, 512);
  fs.writeFileSync(path.join(exportRoot, 'aetherboy-mark-512.png'), applicationArtwork);
  fs.writeFileSync(path.join(applicationBrandingRoot, 'AetherBoyMark.png'), applicationArtwork);
  fs.writeFileSync(
    path.join(applicationBrandingRoot, 'AetherBoy.ico'),
    createIco(frames));

  // Theme edits are checked-in transparent masters. Export only; never
  // regenerate or recolor them as a side effect of building the application.
  const themeRoot = path.join(brandingRoot, 'themes');
  const themeExports = path.join(exportRoot, 'themes');
  fs.mkdirSync(themeExports, { recursive: true });
  const { variants } = JSON.parse(fs.readFileSync(path.join(themeRoot, 'generation.json'), 'utf8'));
  for (const { id } of variants) {
    if (!/^[a-z]+(?:-[a-z]+)*$/.test(id)) throw new Error('Invalid theme asset ID');
    const source = path.join(themeRoot, `${id}.png`);
    if (!(await sharp(source).metadata()).hasAlpha) throw new Error(`Theme ${id} needs alpha`);
    fs.writeFileSync(path.join(themeExports, `${id}.png`), await renderPng(source, 512));
    for (const size of [64, 128]) {
      const sizedRoot = path.join(themeExports, String(size));
      fs.mkdirSync(sizedRoot, { recursive: true });
      fs.writeFileSync(path.join(sizedRoot, `${id}.png`), await renderPng(source, size));
    }
  }

  process.stdout.write(
    `Rendered ${iconSizes.length} icon frames, application artwork, AetherBoy.ico and ${variants.length} theme logos.\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.stack || error}\n`);
  process.exitCode = 1;
});
