// Contact sheet for review only. The PNG masters/exports retain their alpha.
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const themes = [
  ['aether-original', 'Aether Original', '#050712'],
  ['neko-sakura', 'Neko Sakura', '#120B1A'],
  ['deep-ocean', 'Deep Ocean', '#061321'],
  ['emerald-circuit', 'Emerald Circuit', '#061711'],
  ['amber-arcade', 'Amber Arcade', '#1A1010'],
  ['pocket-light', 'Pocket Light', '#F1E9D2'],
];
async function main() {
  const tiles = [];
  for (let i = 0; i < themes.length; i++) {
    const [id, name, background] = themes[i];
    const mark = await sharp(path.join(root, 'branding/exports/themes', `${id}.png`)).resize(420, 420).png().toBuffer();
    const caption = Buffer.from(`<svg width="480" height="72"><text x="240" y="37" text-anchor="middle" fill="${id === 'pocket-light' ? '#284735' : '#F1F4FF'}" font-family="Arial, sans-serif" font-size="24" font-weight="600">${name}</text></svg>`);
    const tile = await sharp({ create: { width: 480, height: 510, channels: 4, background } })
      .composite([{ input: mark, left: 30, top: 12 }, { input: caption, left: 0, top: 436 }]).png().toBuffer();
    tiles.push({ input: tile, left: (i % 2) * 480, top: Math.floor(i / 2) * 510 });
  }
  const output = path.join(root, 'branding/exports/theme-overview.png');
  await sharp({ create: { width: 960, height: 1530, channels: 4, background: '#050712' } })
    .composite(tiles).png().toFile(output);
  console.log(output);
}
main().catch(error => { console.error(error); process.exitCode = 1; });
